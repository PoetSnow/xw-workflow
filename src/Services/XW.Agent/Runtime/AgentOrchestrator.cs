using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Capability;
using XW.Agent.Application.Context;
using XW.Agent.Application.Contracts;
using XW.Agent.Application.Entities;
using XW.Agent.Application.Intents;
using XW.Agent.Application.Llm;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;
using XW.Agent.Application.Planning;
using XW.Agent.Application.Policy;
using XW.Agent.Application.Runtime.Pending;
using XW.Agent.Application.Services;
using XW.Agent.Application.Telemetry;
using XW.Agent.Application.Tooling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;

namespace XW.Agent.Application.Runtime;

/// <summary>
/// Runtime：闲聊短路，其余进 Skill 循环 Retrieve → Plan → Authorize → Execute → Observe。
/// 知识检索是 Skill（knowledge.search），不单独车道。
/// </summary>
public sealed class AgentOrchestrator(
    IAgentSessionStore sessions,
    IAgentLaneRouter laneRouter,
    IIntentRouter intentRouter,
    IAgentPlanner planner,
    IAgentTaskPlanner taskPlanner,
    IProductEntityResolver productResolver,
    ICapabilityRetriever capabilityRetriever,
    IToolRegistry tools,
    ICapabilityCatalog capabilities,
    IWorkflowExecutor workflowExecutor,
    IToolResultEvaluator toolResultEvaluator,
    IPermissionPolicy permissionPolicy,
    IApprovalGate approvalGate,
    IAgentAuditStore audit,
    IAgentTurnStore turns,
    IAgentLlmRuntime llmRuntime,
    IReplyComposer replyComposer,
    IUserContextAccessor userContextAccessor,
    IAgentTurnTelemetry turnTelemetry,
    IPendingTaskDispatcher pendingTasks,
    IOptionsMonitor<AgentOptions> options,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
    /// <inheritdoc />
    /// <remarks>
    /// 主路径：闲聊短路 | Skill 循环 → Reply；审计落库。
    /// </remarks>
    public async Task<AgentChatResponse> ChatAsync(AgentChatRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var user = userContextAccessor.Current
                   ?? new UserContext
                   {
                       UserId = request.UserId,
                       TenantId = request.TenantId,
                       PermissionCodes = request.PermissionCodes ?? [],
                       Roles = request.Roles ?? [],
                       AccessToken = request.AccessToken,
                       Confirmed = request.Confirmed,
                       TraceId = Guid.NewGuid().ToString("N")
                   };

        var traceId = string.IsNullOrWhiteSpace(user.TraceId)
            ? Guid.NewGuid().ToString("N")
            : user.TraceId;

        turnTelemetry.Begin(traceId);

        var session = await sessions.GetOrCreateAsync(request.SessionId, user.UserId ?? request.UserId, ct);
        if (request.CorpusIds is not null)
        {
            SessionCorpus.Write(session, request.CorpusIds);
        }

        session.Messages.Add(new AgentMessage { Role = "user", Content = request.Message });
        session.Slots["_confirmed"] = user.Confirmed ? "true" : "false";

        using var chatActivity = AgentTelemetry.StartChat(
            traceId,
            session.Id,
            user.UserId ?? session.UserId,
            user.TenantId);

        var toolCalls = new List<AgentToolCallLogDto>();
        var permissionDenied = false;
        var needsApproval = false;
        // 相位已写进遥测后，普通异常不再覆盖它；截止时间和槽位冲突始终改成对应相位。
        var phaseCommitted = false;
        AgentTurnSnapshot? snap = null;

        try
        {
        IntentResult intent;
        string phase;
        string? toolSummary;
        string fallback;
        string knowledgeText = "(无命中知识)";

        try
        {
            // ① 意图/车道路由（先分流，再进 Knowledge 或 Action）
            var route = await laneRouter.RouteAsync(session, request.Message, ct);
            turnTelemetry.SetLane(route.Lane.ToString(), route.Reason);
            turnTelemetry.AddStep("route", route.Lane.ToString(), 0, route.Reason, true);
            chatActivity?.SetTag("agent.route.lane", route.Lane.ToString());
            chatActivity?.SetTag("agent.route.reason", route.Reason);
            chatActivity?.SetTag("agent.route.confidence", route.Confidence);
            logger.LogInformation(
                "Lane route lane={Lane} intent={Intent} conf={Conf:0.##} reason={Reason}",
                route.Lane, route.Intent, route.Confidence, route.Reason);

            switch (route.Lane)
            {
                case AgentRouteLane.Chitchat:
                    intent = new IntentResult
                    {
                        Intent = route.Intent,
                        Confidence = route.Confidence,
                        ReplyHint = "简短礼貌回应，并说明可以查业务数据、检索知识库或执行已登记的能力。"
                    };
                    phase = "chitchat";
                    toolSummary = null;
                    fallback = intent.ReplyHint!;
                    knowledgeText = "(无命中知识)";
                    break;

                default:
                    knowledgeText = "(无命中知识)";
                    if (options.CurrentValue.Planner.Enabled)
                    {
                        (intent, phase, toolSummary, fallback, permissionDenied, needsApproval) =
                            await RunPlannerLoopAsync(session, request, user, toolCalls, knowledgeText, ct);
                    }
                    else
                    {
                        (intent, phase, toolSummary, fallback, permissionDenied, needsApproval) =
                            await RunLegacyIntentPathAsync(session, request, user, toolCalls, ct);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            AgentTelemetry.SetError(chatActivity, ex);
            throw;
        }

        if (string.Equals(phase, "tool_ok", StringComparison.OrdinalIgnoreCase) && toolCalls.Count > 0)
        {
            var last = toolCalls.LastOrDefault(t => t.Success);
            var lastCap = last is null ? null : capabilities.FindByCode(last.ToolName);
            if (lastCap is not null
                && string.Equals(lastCap.Domain, "knowledge", StringComparison.OrdinalIgnoreCase))
            {
                var miss = last!.Summary?.Contains("未命中", StringComparison.Ordinal) == true;
                intent = new IntentResult
                {
                    Intent = "knowledge.answer",
                    Confidence = miss ? 0.6 : 0.92,
                    ReplyHint = fallback
                };
                phase = miss ? "knowledge_miss" : "knowledge_ok";
                knowledgeText = last.Summary ?? knowledgeText;
            }
        }

        chatActivity?.SetTag(AgentTelemetry.Tags.Phase, phase);
        chatActivity?.SetTag(AgentTelemetry.Tags.Intent, intent.Intent);
        chatActivity?.SetTag(AgentTelemetry.Tags.PermissionDenied, permissionDenied);
        chatActivity?.SetTag(AgentTelemetry.Tags.ApprovalRequired, needsApproval);
        chatActivity?.SetTag("agent.tool_call_count", toolCalls.Count);
        AgentTelemetry.SetOk(chatActivity, !permissionDenied && phase is not ("tool_error" or "denied"));
        turnTelemetry.SetPhase(phase);
        phaseCommitted = true;

        var reply = await replyComposer.ComposeAsync(new ReplyComposeRequest
        {
            Session = session,
            UserMessage = request.Message,
            Intent = intent,
            Phase = phase,
            ToolSummary = toolSummary,
            KnowledgeSummary = knowledgeText,
            FallbackReply = fallback,
            Capabilities = capabilities.ListEnabled().ToList()
        }, ct);

        session.Messages.Add(new AgentMessage { Role = "assistant", Content = reply, Intent = intent.Intent });
        session.TenantId = user.TenantId ?? session.TenantId;
        await sessions.SaveAsync(session, ct);

        await audit.AppendAsync(new AgentAuditRecord
        {
            TraceId = traceId,
            SessionId = session.Id,
            UserId = user.UserId ?? session.UserId,
            TenantId = user.TenantId,
            Intent = intent.Intent,
            UserMessage = request.Message,
            AssistantReply = reply,
            PermissionDenied = permissionDenied,
            ApprovalRequired = needsApproval,
            ToolCalls = toolCalls.Select(t => new ToolExecutionResult
            {
                ToolName = t.ToolName,
                Success = t.Success,
                Summary = t.Summary ?? string.Empty,
                ElapsedMs = t.ElapsedMs
            }).ToList()
        }, ct);

        sw.Stop();
        snap = turnTelemetry.Snapshot(sw.ElapsedMilliseconds);
        var analysis = BuildTurnAnalysis(snap, toolCalls);

        return new AgentChatResponse(
            session.Id,
            reply,
            intent.Intent,
            intent.Confidence,
            intent.NeedsClarification || string.Equals(phase, "clarify", StringComparison.OrdinalIgnoreCase),
            needsApproval,
            intent.MissingSlots,
            toolCalls,
            traceId,
            user.TenantId,
            user.UserId ?? session.UserId,
            analysis);
        }
        catch (Exception ex)
        {
            // 截止时间和槽位冲突改相位；其余失败保留已经提交的业务相位，并补一条 failure 步骤。
            var failurePhase = ex switch
            {
                OperationCanceledException => "deadline",
                SessionConcurrencyException => "conflict",
                _ => phaseCommitted ? null : "error"
            };
            if (failurePhase is not null)
            {
                turnTelemetry.SetPhase(failurePhase);
            }

            turnTelemetry.AddStep("failure", ex.GetType().Name, 0, TruncateForTurn(ex.Message), false);
            AgentTelemetry.SetError(chatActivity, ex);
            throw;
        }
        finally
        {
            // 取消后的插入不能再用请求 Token，否则截止时间会把轮次行一起取消掉。
            sw.Stop();
            snap ??= turnTelemetry.Snapshot(sw.ElapsedMilliseconds);
            try
            {
                var persisted = BuildTurnAnalysis(snap, toolCalls);
                await turns.AppendAsync(new AgentTurnRecord
                {
                    TraceId = traceId,
                    SessionId = session.Id,
                    Lane = snap.Lane,
                    LaneReason = snap.LaneReason,
                    Phase = snap.Phase,
                    AnalysisJson = System.Text.Json.JsonSerializer.Serialize(persisted),
                    CreatedAt = DateTimeOffset.UtcNow
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "agent_turn 写入失败 traceId={TraceId}", traceId);
            }

            AgentTelemetry.RecordChat(snap.Lane, snap.Phase, snap.TotalElapsedMs);
            AgentTelemetry.RecordLlmTokens(snap.TotalTokens, snap.Lane);
            if (permissionDenied)
            {
                AgentTelemetry.RecordPermissionDenied(snap.Lane);
            }

            if (needsApproval)
            {
                AgentTelemetry.RecordApprovalRequired(snap.Lane);
            }

            turnTelemetry.Clear();
        }
    }

    /// <summary>响应和轮次表共用同一份分析，避免失败路径和成功路径字段不一致。</summary>
    private static AgentTurnAnalysisDto BuildTurnAnalysis(
        AgentTurnSnapshot snap,
        List<AgentToolCallLogDto> toolCalls) =>
        new(
            snap.TotalElapsedMs,
            snap.Lane,
            snap.LaneReason,
            snap.Phase,
            snap.PromptTokens,
            snap.CompletionTokens,
            snap.TotalTokens,
            snap.Steps
                .Select(s => new AgentAnalysisStepDto(s.Kind, s.Name, s.ElapsedMs, s.Detail, s.Success))
                .Concat(toolCalls.Select(t =>
                    new AgentAnalysisStepDto("tool", t.ToolName, t.ElapsedMs, t.Summary, t.Success)))
                .ToList(),
            snap.LlmCalls.Select(c => new AgentLlmUsageDto(
                c.CallId, c.Profile, c.Model, c.PromptTokens, c.CompletionTokens, c.TotalTokens, c.ElapsedMs)).ToList());

    private static string TruncateForTurn(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        return message.Length <= 200 ? message : message[..200];
    }

    private async Task<(
        IntentResult Intent,
        string Phase,
        string? ToolSummary,
        string Fallback,
        bool PermissionDenied,
        bool NeedsApproval)> RunPlannerLoopAsync(
        AgentSession session,
        AgentChatRequest request,
        UserContext user,
        List<AgentToolCallLogDto> toolCalls,
        string knowledgeText,
        CancellationToken ct)
    {
        var sm = new AgentStateMachine();
        var maxSteps = Math.Clamp(options.CurrentValue.Planner.MaxSteps, 1, 20);
        var retrievalCfg = options.CurrentValue.Planner.Retrieval;
        var observations = new List<AgentPlanObservation>();
        var summaries = new List<string>();
        var permissionDenied = false;
        var needsApproval = false;

        var allowedCaps = capabilities.ListEnabled()
            .Where(c => permissionPolicy.IsAllowedAll(user, c.ResolvePermissions()))
            .Where(c => AgentSurfacePolicy.Allow(c, user, permissionPolicy))
            .ToList();

        sm.Transition(AgentRunState.Retrieve, "permission_filter");

        if (allowedCaps.Count == 0 && capabilities.ListEnabled().Count > 0)
        {
            permissionDenied = true;
            sm.Transition(AgentRunState.Deny, "no_permission");
            sm.Transition(AgentRunState.Reply);
            return (
                new IntentResult
                {
                    Intent = "agent_plan",
                    Confidence = 1,
                    ReplyHint = "权限不足，无法使用业务能力。"
                },
                "denied",
                null,
                "权限不足：当前身份没有可用的业务能力权限。",
                true,
                false);
        }

        // —— 待办任务优先：有挂起则按 Handler 推进，不再重跑 TaskPlan 猜意图 ——
        var pendingReply = PendingTaskState.ClassifyReply(session, request.Message);
        if (pendingReply == PendingTaskState.UserReplyKind.Cancel)
        {
            pendingTasks.Cancel(session);
            sm.Transition(AgentRunState.Reply, "pending_cancelled");
            return (
                new IntentResult
                {
                    Intent = "purchase_create",
                    Confidence = 1,
                    ReplyHint = "已取消当前采购待办。"
                },
                "tool_ok",
                "pending_cancelled",
                "已取消当前采购待办。",
                permissionDenied,
                needsApproval);
        }

        var resumePending =
            (pendingReply is PendingTaskState.UserReplyKind.Affirm
                or PendingTaskState.UserReplyKind.ProvideSlots
                or PendingTaskState.UserReplyKind.SelectOption)
            && PendingTaskState.HasPending(session);

        AgentPlanDecision? pendingForcedDecision = null;
        if (resumePending)
        {
            session.Slots["_task_intent"] = PendingTaskState.GetKind(session) ?? PendingTaskState.KindPurchaseCreate;

            // 仅最终确认的 Affirm 才放行 ConfirmRequired；补槽/选商不抬确认
            if (PendingTaskSnapshot.ShouldElevateWriteConfirmation(
                    session,
                    (PendingTaskReplyKind)(int)pendingReply))
            {
                PendingTaskSnapshot.MarkWriteConfirmed(session);
                user = WithConfirmed(user, confirmed: true);
                userContextAccessor.Current = user;
            }

            var replyKind = (PendingTaskReplyKind)(int)pendingReply;
            if (pendingTasks.TryPlan(session, replyKind, allowedCaps, out var pendingDecision))
            {
                pendingForcedDecision = pendingDecision;
            }

            turnTelemetry.AddStep(
                "pending",
                PendingTaskState.GetStep(session) ?? PendingTaskState.StepAwaitConfirm,
                0,
                $"resume kind={PendingTaskState.GetKind(session)} reply={pendingReply}",
                true);
        }
        else
        {
            if (PendingTaskState.HasPending(session)
                && pendingReply == PendingTaskState.UserReplyKind.Unrelated)
            {
                pendingTasks.Cancel(session);
            }

            var taskPlan = await taskPlanner.CreatePlanAsync(session, request.Message, ct);
            ApplyTaskPlanToSession(session, taskPlan);
            turnTelemetry.AddStep(
                "task_plan",
                taskPlan.Intent,
                0,
                $"entities=[{string.Join(",", taskPlan.Entities.Select(kv => $"{kv.Key}={kv.Value}"))}] steps=[{string.Join("→", taskPlan.SuggestedSteps)}]",
                true);

            if (taskPlan.Entities.TryGetValue("product", out var productName)
                && !string.IsNullOrWhiteSpace(productName)
                && !session.Slots.ContainsKey("productId")
                && !session.Slots.ContainsKey("materialId"))
            {
                sm.Transition(AgentRunState.Plan, "resolve_product");
                var resolved = await productResolver.ResolveAsync(session, productName, ct);
                toolCalls.Add(new AgentToolCallLogDto(
                    "entity.product.resolve",
                    resolved.Success,
                    resolved.Summary ?? resolved.Question,
                    0));
                turnTelemetry.AddStep(
                    "resolve",
                    "product",
                    0,
                    resolved.Success
                        ? $"id={resolved.ProductId ?? resolved.MaterialId} name={resolved.Name} conf={resolved.Confidence:0.##}"
                        : resolved.Question,
                    resolved.Success);

                if (resolved.NeedConfirm)
                {
                    var optionsText = resolved.Options.Count == 0
                        ? string.Empty
                        : "\n" + string.Join("\n", resolved.Options.Select((o, i) =>
                            $"{i + 1}. {o.Name}" + (string.IsNullOrWhiteSpace(o.Sku) ? "" : $"（{o.Sku}）")));
                    sm.Transition(AgentRunState.Clarify, "product_ambiguous");
                    sm.Transition(AgentRunState.Reply);
                    return (
                        new IntentResult
                        {
                            Intent = taskPlan.Intent,
                            Confidence = taskPlan.Confidence,
                            ForceClarification = true,
                            ReplyHint = (resolved.Question ?? "请确认商品") + optionsText,
                            MissingSlots = ["product"]
                        },
                        "clarify",
                        JoinSummaries(summaries),
                        (resolved.Question ?? "请确认商品") + optionsText,
                        permissionDenied,
                        needsApproval);
                }

                if (resolved.Success)
                {
                    ApplyProductResolveToSession(session, resolved);
                }
                else
                {
                    sm.Transition(AgentRunState.Clarify, "product_unresolved");
                    sm.Transition(AgentRunState.Reply);
                    return (
                        new IntentResult
                        {
                            Intent = taskPlan.Intent,
                            Confidence = taskPlan.Confidence,
                            ForceClarification = true,
                            ReplyHint = resolved.Question ?? "未能解析商品。",
                            MissingSlots = ["product"]
                        },
                        "clarify",
                        JoinSummaries(summaries),
                        resolved.Question ?? "未能解析商品。",
                        permissionDenied,
                        needsApproval);
                }
            }
        }

        // 待办 Handler 已给出 clarify（如最终确认文案 / 选供应商）：本轮直接回复
        if (pendingForcedDecision is not null
            && string.Equals(pendingForcedDecision.Action, "clarify", StringComparison.OrdinalIgnoreCase))
        {
            var q = pendingForcedDecision.Question ?? "请确认下一步。";
            sm.Transition(AgentRunState.Clarify, pendingForcedDecision.Reason ?? "pending");
            sm.Transition(AgentRunState.Reply);
            return (
                new IntentResult
                {
                    Intent = PendingTaskState.GetKind(session) ?? "agent_plan",
                    Confidence = 1,
                    ForceClarification = true,
                    ReplyHint = q
                },
                "clarify",
                JoinSummaries(summaries),
                q,
                permissionDenied,
                needsApproval);
        }

        for (var step = 0; step < maxSteps; step++)
        {
            // 截止时间到了就停，避免规划失败被吞掉后继续调下一个工具。
            ct.ThrowIfCancellationRequested();

            // 任一步已鉴权失败：立即结束，禁止再调 Planner（省掉十几秒 LLM + 乱 clarify）
            if (TryGetAuthTerminal(observations, out var authPhase, out var authFallback))
            {
                logger.LogWarning("Skill loop stop on auth failure step={Step}", step);
                sm.Transition(AgentRunState.Reply, authPhase);
                return (
                    new IntentResult
                    {
                        Intent = "agent_plan",
                        Confidence = 1,
                        ReplyHint = authFallback
                    },
                    authPhase,
                    JoinSummaries(summaries) ?? authFallback,
                    authFallback,
                    permissionDenied,
                    needsApproval);
            }

            // 回到 Retrieve：Observe 后继续，或 Plan 空转后再检索
            if (sm.State == AgentRunState.Observe || sm.State == AgentRunState.Plan)
            {
                sm.Transition(AgentRunState.Retrieve, $"step_{step}");
            }

            IReadOnlyList<AgentCapabilityDefinition> planTools;
            if (retrievalCfg.Enabled)
            {
                using var retrieveActivity = AgentTelemetry.StartState("retrieve", step);
                var keep = observations.Select(o => o.Tool)
                    .Concat(allowedCaps
                        .Where(c => string.Equals(c.Domain, "knowledge", StringComparison.OrdinalIgnoreCase))
                        .Select(c => c.Code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var retrieved = await capabilityRetriever.RetrieveAsync(new CapabilityRetrievalRequest
                {
                    Query = request.Message,
                    Candidates = allowedCaps,
                    KeepCodes = keep,
                    TopK = retrievalCfg.TopK,
                    ExpandRelated = retrievalCfg.ExpandRelated,
                    MinScore = retrievalCfg.MinScore
                }, ct);
                planTools = retrieved.Tools;
                retrieveActivity?.SetTag(AgentTelemetry.Tags.RetrieveHitCount, planTools.Count);
                retrieveActivity?.SetTag(AgentTelemetry.Tags.RetrieveCandidateCount, retrieved.CandidateCount);
                AgentTelemetry.SetOk(retrieveActivity, planTools.Count > 0);
                logger.LogInformation(
                    "State={State} retrieved {Count}/{Cand} tools for planner: [{Tools}]",
                    sm.State,
                    planTools.Count,
                    retrieved.CandidateCount,
                    string.Join(",", planTools.Select(t => t.Code)));
            }
            else
            {
                planTools = allowedCaps;
            }

            if (planTools.Count == 0)
            {
                sm.Transition(AgentRunState.Clarify, "empty_retrieve");
                sm.Transition(AgentRunState.Reply);
                return (
                    new IntentResult
                    {
                        Intent = "agent_plan",
                        Confidence = 1,
                        ForceClarification = true,
                        ReplyHint = "暂时找不到匹配的业务能力，请换一种说法。"
                    },
                    "clarify",
                    JoinSummaries(summaries),
                    "暂时找不到匹配的业务能力，请换一种说法（例如：查 SKU1001 库存）。",
                    permissionDenied,
                    needsApproval);
            }

            sm.Transition(AgentRunState.Plan, $"tools={planTools.Count}");
            AgentPlanDecision decision;
            using (var planActivity = AgentTelemetry.StartState("plan", step))
            {
                planActivity?.SetTag("agent.plan.tool_count", planTools.Count);

                if (observations.Count > 0
                    && PendingTaskState.HasPending(session)
                    && pendingTasks.TryAfterTool(
                        session, allowedCaps, observations, summaries, out var afterPending))
                {
                    decision = afterPending;
                    planActivity?.SetTag("agent.plan.forced", afterPending.Tool ?? afterPending.Action);
                    logger.LogInformation(
                        "Plan bypass LLM → pending after-tool action={Action} tool={Tool}",
                        afterPending.Action, afterPending.Tool ?? "-");
                }
                else if (observations.Count == 0 && pendingForcedDecision is not null)
                {
                    decision = pendingForcedDecision;
                    pendingForcedDecision = null;
                    planActivity?.SetTag("agent.plan.forced", decision.Tool ?? decision.Action);
                    logger.LogInformation(
                        "Plan bypass LLM → pending forced action={Action} tool={Tool}",
                        decision.Action, decision.Tool ?? "-");
                }
                else
                {
                    decision = await planner.NextAsync(
                        session, request.Message, planTools, observations, knowledgeText, ct);
                }

                planActivity?.SetTag(AgentTelemetry.Tags.Action, decision.Action);
                if (!string.IsNullOrWhiteSpace(decision.Tool))
                {
                    planActivity?.SetTag(AgentTelemetry.Tags.Capability, decision.Tool);
                }

                AgentTelemetry.SetOk(planActivity, true);
            }

            var action = decision.Action.Trim().ToLowerInvariant();

            if (action == "clarify")
            {
                // Planner 在鉴权失败后常乱 clarify；有失败摘要时以事实为准结束
                if (TryGetAuthTerminal(observations, out var authPhase2, out var authFallback2))
                {
                    sm.Transition(AgentRunState.Reply, authPhase2);
                    return (
                        new IntentResult { Intent = "agent_plan", Confidence = 1, ReplyHint = authFallback2 },
                        authPhase2,
                        JoinSummaries(summaries) ?? authFallback2,
                        authFallback2,
                        permissionDenied,
                        needsApproval);
                }

                else if (TryFinishFromSuccessfulObservations(observations, summaries, out var rescueFinish))
                {
                    logger.LogInformation(
                        "Clarify rescued by tool facts reason={Reason}",
                        decision.Reason ?? "-");
                    sm.Transition(AgentRunState.Reply, "tool_ok");
                    var rescueText = rescueFinish.Reason ?? JoinSummaries(summaries)!;
                    return (
                        new IntentResult
                        {
                            Intent = "agent_plan",
                            Confidence = 1,
                            ReplyHint = rescueText
                        },
                        "tool_ok",
                        JoinSummaries(summaries) ?? rescueText,
                        rescueText,
                        permissionDenied,
                        needsApproval);
                }

                if (action == "clarify")
                {
                    sm.Transition(AgentRunState.Clarify, decision.Reason);
                    sm.Transition(AgentRunState.Reply);
                    return (
                        new IntentResult
                        {
                            Intent = "agent_plan",
                            Confidence = 1,
                            ForceClarification = true,
                            ReplyHint = decision.Question,
                            Slots = decision.Arguments
                        },
                        "clarify",
                        JoinSummaries(summaries),
                        decision.Question ?? "请再补充一点信息。",
                        permissionDenied,
                        needsApproval);
                }
            }

            if (action == "confirm")
            {
                sm.Transition(AgentRunState.Confirm, decision.Reason);
                sm.Transition(AgentRunState.Reply);
                needsApproval = true;
                return (
                    new IntentResult
                    {
                        Intent = "agent_plan",
                        Confidence = 1,
                        ReplyHint = decision.Question
                    },
                    "approval",
                    JoinSummaries(summaries),
                    decision.Question ?? "该操作需要确认后再执行。",
                    permissionDenied,
                    true);
            }

            if (action == "finish")
            {
                var okSummary = JoinSummaries(summaries);
                var hasKnowledge = !string.IsNullOrWhiteSpace(knowledgeText)
                                   && !knowledgeText.Contains("(无命中知识)", StringComparison.Ordinal);
                // 确定性 finish（如库存+采购结论）写在 Reason，优先于纯工具摘要
                var userFacing = !string.IsNullOrWhiteSpace(decision.Reason)
                                 && (string.IsNullOrWhiteSpace(okSummary)
                                     || decision.Reason!.Contains("结论：", StringComparison.Ordinal)
                                     || (okSummary is not null
                                         && decision.Reason.Contains(okSummary, StringComparison.Ordinal)))
                    ? decision.Reason!
                    : okSummary
                      ?? (hasKnowledge
                          ? "根据知识库相关说明回答。"
                          : (decision.Reason ?? "好的。"));
                sm.Transition(AgentRunState.Reply, "finish");
                return (
                    new IntentResult
                    {
                        Intent = "agent_plan",
                        Confidence = 1,
                        Slots = decision.Arguments,
                        ReplyHint = userFacing
                    },
                    summaries.Count > 0 ? "tool_ok" : hasKnowledge ? "tool_ok" : "chitchat",
                    okSummary ?? (hasKnowledge ? "已参考知识库片段作答。" : null),
                    userFacing,
                    permissionDenied,
                    needsApproval);
            }

            if (action != "call_tool" || string.IsNullOrWhiteSpace(decision.Tool))
            {
                continue;
            }

            // 硬约束：同参已成功 / 鉴权失败 / 同参已失败 → 禁止再 call_tool
            if (TryStopDeadLoop(observations, decision, out var stopPhase, out var stopFallback))
            {
                if (string.Equals(stopPhase, "already_answered", StringComparison.Ordinal))
                {
                    logger.LogInformation(
                        "Planner stop: tool={Tool} already succeeded with same args",
                        decision.Tool);
                }
                else
                {
                    logger.LogWarning(
                        "Planner dead-loop stopped: tool={Tool} phase={Phase}",
                        decision.Tool, stopPhase);
                }

                sm.Transition(AgentRunState.Reply, stopPhase);
                var phaseOut = string.Equals(stopPhase, "already_answered", StringComparison.Ordinal)
                    ? "tool_ok"
                    : stopPhase;
                return (
                    new IntentResult
                    {
                        Intent = "agent_plan",
                        Confidence = 1,
                        ReplyHint = stopFallback
                    },
                    phaseOut,
                    JoinSummaries(summaries) ?? stopFallback,
                    stopFallback,
                    permissionDenied,
                    needsApproval);
            }

            sm.Transition(AgentRunState.Authorize, decision.Tool);
            var cap = capabilities.FindByCode(decision.Tool!);
            var requiredPerms = cap?.ResolvePermissions() ?? [];

            if (cap is null
                || !allowedCaps.Any(c => string.Equals(c.Code, cap.Code, StringComparison.OrdinalIgnoreCase)))
            {
                if (cap is not null && !permissionPolicy.IsAllowedAll(user, requiredPerms))
                {
                    permissionDenied = true;
                    sm.Transition(AgentRunState.Deny, string.Join(",", requiredPerms));
                    sm.Transition(AgentRunState.Reply);
                    return (
                        new IntentResult { Intent = "agent_plan", Confidence = 1 },
                        "denied",
                        JoinSummaries(summaries),
                        $"权限不足：需要 `{string.Join(" + ", requiredPerms)}`。",
                        true,
                        needsApproval);
                }

                observations.Add(new AgentPlanObservation
                {
                    Tool = decision.Tool!,
                    Success = false,
                    Error = "unknown_capability",
                    Summary = $"能力 {decision.Tool} 不存在或不可用"
                });
                sm.Transition(AgentRunState.Observe, "unknown_tool");
                continue;
            }

            if (!permissionPolicy.IsAllowedAll(user, requiredPerms))
            {
                permissionDenied = true;
                sm.Transition(AgentRunState.Deny, string.Join(",", requiredPerms));
                sm.Transition(AgentRunState.Reply);
                return (
                    new IntentResult { Intent = "agent_plan", Confidence = 1 },
                    "denied",
                    JoinSummaries(summaries),
                    $"权限不足：需要 `{string.Join(" + ", requiredPerms)}`。",
                    true,
                    needsApproval);
            }

            var approval = approvalGate.Evaluate(cap.ConfirmRequired, user);
            // 仅 Pending 最终确认抬升的 _confirmed 可旁路 ConfirmRequired
            if (!approval.Allowed
                && cap.ConfirmRequired
                && PendingTaskSnapshot.IsWriteConfirmed(session))
            {
                approval = new ApprovalDecision(true, null);
            }

            if (!approval.Allowed)
            {
                needsApproval = true;
                sm.Transition(AgentRunState.Confirm, "policy");
                sm.Transition(AgentRunState.Reply);
                return (
                    new IntentResult { Intent = "agent_plan", Confidence = 1 },
                    "approval",
                    JoinSummaries(summaries),
                    approval.Message
                    ?? "该操作需要确认。请回复「确认创建」继续。",
                    permissionDenied,
                    true);
            }

            foreach (var (k, v) in decision.Arguments)
            {
                if (string.Equals(k, SessionCorpus.SlotKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(v))
                {
                    var value = v.Trim();
                    if (string.Equals(k, "keywords", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(k, "keyword", StringComparison.OrdinalIgnoreCase))
                    {
                        value = SanitizeProductKeywords(value);
                    }

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        session.Slots[k] = value;
                        decision.Arguments[k] = value;
                    }
                }
            }

            var entryKind = cap.Entry?.Kind?.Trim().ToLowerInvariant() ?? "tool";
            var toolCode = string.Equals(entryKind, "tool", StringComparison.OrdinalIgnoreCase)
                           && !string.IsNullOrWhiteSpace(cap.Entry?.Target)
                ? cap.Entry!.Target.Trim()
                : cap.Code;

            sm.Transition(AgentRunState.Execute, cap.Code);
            using var execActivity = AgentTelemetry.StartState("execute", step);
            execActivity?.SetTag(AgentTelemetry.Tags.Capability, cap.Code);
            execActivity?.SetTag("agent.entry.kind", entryKind);

            if (string.Equals(entryKind, "workflow", StringComparison.OrdinalIgnoreCase))
            {
                var wfCode = cap.Entry?.Target?.Trim() ?? string.Empty;
                var workflow = options.CurrentValue.Workflows.FirstOrDefault(w =>
                    w.Enabled && string.Equals(w.Code, wfCode, StringComparison.OrdinalIgnoreCase));
                if (workflow is null)
                {
                    observations.Add(new AgentPlanObservation
                    {
                        Tool = cap.Code,
                        Success = false,
                        Error = "workflow_missing",
                        Summary = $"业务能力 {cap.Code} 绑定的工作流 {wfCode} 不存在"
                    });
                    sm.Transition(AgentRunState.Observe, "workflow_missing");
                    continue;
                }

                execActivity?.SetTag(AgentTelemetry.Tags.Tool, wfCode);
                var wfResult = await workflowExecutor.ExecuteAsync(
                    workflow, session, decision.Arguments, ct);
                execActivity?.SetTag("agent.tool.success", wfResult.Success);
                execActivity?.SetTag("agent.tool.elapsed_ms", wfResult.ElapsedMs);
                AgentTelemetry.SetOk(execActivity, wfResult.Success);
                toolCalls.AddRange(wfResult.StepLogs);
                toolCalls.Add(new AgentToolCallLogDto(cap.Code, wfResult.Success, wfResult.Summary, wfResult.ElapsedMs));
                AgentTelemetry.RecordTool(cap.Code, wfResult.Success, wfResult.ElapsedMs, wfResult.HttpStatusCode);

                // ★ Tool Result Evaluator：空结果/鉴权/失败 → 确定性相位，不再进 Planner/Reply LLM 编造
                var wfEval = toolResultEvaluator.EvaluateWorkflow(wfResult);
                logger.LogInformation(
                    "ToolResultEvaluator workflow={Wf} kind={Kind} phase={Phase}",
                    wfCode, wfEval.Kind, wfEval.Phase);

                if (!wfEval.AllowLlmReply)
                {
                    observations.Add(new AgentPlanObservation
                    {
                        Tool = cap.Code,
                        Success = false,
                        Summary = wfResult.Summary,
                        Preview = wfResult.Summary,
                        Error = wfResult.Error ?? wfEval.Kind.ToString(),
                        HttpStatusCode = wfResult.HttpStatusCode,
                        ErrorKind = wfResult.ErrorKind ?? wfEval.Kind.ToString().ToLowerInvariant(),
                        Arguments = new Dictionary<string, string>(decision.Arguments, StringComparer.OrdinalIgnoreCase)
                    });
                    sm.Transition(AgentRunState.Observe, wfEval.Phase);
                    if (wfEval.Kind == ToolResultKind.Empty)
                    {
                        sm.Transition(AgentRunState.Clarify, "empty");
                    }

                    sm.Transition(AgentRunState.Reply, wfEval.Phase);
                    return (
                        new IntentResult
                        {
                            Intent = "agent_plan",
                            Confidence = 1,
                            ForceClarification = wfEval.Kind == ToolResultKind.Empty,
                            ReplyHint = wfEval.UserMessage
                        },
                        wfEval.Phase,
                        wfResult.Summary,
                        wfEval.UserMessage,
                        permissionDenied,
                        needsApproval);
                }

                summaries.Add(wfResult.Summary);
                pendingTasks.NotifyToolSuccess(session, cap.Code, data: null);
                observations.Add(new AgentPlanObservation
                {
                    Tool = cap.Code,
                    Success = true,
                    Summary = wfResult.Summary,
                    Preview = wfResult.Summary,
                    Arguments = new Dictionary<string, string>(decision.Arguments, StringComparer.OrdinalIgnoreCase)
                });
                sm.Transition(AgentRunState.Observe, "ok");
                continue;
            }

            var tool = tools.FindByCode(toolCode);
            if (tool is null)
            {
                observations.Add(new AgentPlanObservation
                {
                    Tool = cap.Code,
                    Success = false,
                    Error = "no_tool",
                    Summary = $"能力 {cap.Code} 无法执行（tool={toolCode}）"
                });
                sm.Transition(AgentRunState.Observe, "no_tool");
                continue;
            }

            var intentForTool = new IntentResult
            {
                Intent = cap.IntentCode ?? toolCode,
                Confidence = 1,
                Slots = new Dictionary<string, string>(decision.Arguments, StringComparer.OrdinalIgnoreCase)
            };

            execActivity?.SetTag(AgentTelemetry.Tags.Tool, toolCode);
            var exec = await tool.ExecuteAsync(session, intentForTool, ct);
            execActivity?.SetTag("agent.tool.success", exec.Success);
            execActivity?.SetTag("agent.tool.elapsed_ms", exec.ElapsedMs);
            AgentTelemetry.SetOk(execActivity, exec.Success);
            toolCalls.Add(new AgentToolCallLogDto(exec.ToolName, exec.Success, exec.Summary, exec.ElapsedMs));
            AgentTelemetry.RecordTool(exec.ToolName, exec.Success, exec.ElapsedMs, exec.HttpStatusCode);

            var toolEval = toolResultEvaluator.EvaluateTool(exec);
            logger.LogInformation(
                "ToolResultEvaluator tool={Tool} kind={Kind} phase={Phase}",
                cap.Code, toolEval.Kind, toolEval.Phase);

            if (!toolEval.AllowLlmReply)
            {
                observations.Add(new AgentPlanObservation
                {
                    Tool = cap.Code,
                    Success = false,
                    Summary = exec.Summary,
                    Preview = ExtractPreview(exec.Data),
                    Error = exec.Error ?? toolEval.Kind.ToString(),
                    HttpStatusCode = exec.HttpStatusCode,
                    ErrorKind = exec.Success ? null : ClassifyError(exec.Error, exec.HttpStatusCode),
                    Arguments = new Dictionary<string, string>(decision.Arguments, StringComparer.OrdinalIgnoreCase)
                });
                sm.Transition(AgentRunState.Observe, toolEval.Phase);
                if (toolEval.Kind == ToolResultKind.Empty)
                {
                    sm.Transition(AgentRunState.Clarify, "empty");
                }

                sm.Transition(AgentRunState.Reply, toolEval.Phase);
                return (
                    new IntentResult
                    {
                        Intent = "agent_plan",
                        Confidence = 1,
                        ForceClarification = toolEval.Kind == ToolResultKind.Empty,
                        ReplyHint = toolEval.UserMessage
                    },
                    toolEval.Phase,
                    exec.Summary,
                    toolEval.UserMessage,
                    permissionDenied,
                    needsApproval);
            }

            summaries.Add(exec.Summary);
            pendingTasks.NotifyToolSuccess(session, cap.Code, exec.Data);

            observations.Add(new AgentPlanObservation
            {
                Tool = cap.Code,
                Success = true,
                Summary = exec.Summary,
                Preview = ExtractPreview(exec.Data),
                Arguments = new Dictionary<string, string>(decision.Arguments, StringComparer.OrdinalIgnoreCase)
            });
            sm.Transition(AgentRunState.Observe, "ok");
        }

        var finalSummary = JoinSummaries(summaries);
        if (sm.State is not AgentRunState.Reply and not AgentRunState.Done)
        {
            // Observe/Plan 等 → Reply；非法则直接记日志
            try
            {
                if (sm.State == AgentRunState.Observe)
                {
                    sm.Transition(AgentRunState.Retrieve, "max_steps");
                    sm.Transition(AgentRunState.Plan, "max_steps");
                }

                if (sm.State == AgentRunState.Plan)
                {
                    sm.Transition(AgentRunState.Reply, "max_steps");
                }
                else if (sm.State == AgentRunState.Retrieve)
                {
                    sm.Transition(AgentRunState.Plan, "max_steps");
                    sm.Transition(AgentRunState.Reply, "max_steps");
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "State machine end transition skipped from {State}", sm.State);
            }
        }

        logger.LogInformation("Agent state history: {History}",
            string.Join(" → ", sm.History.Select(h => $"{h.From}->{h.To}")));

        return (
            new IntentResult { Intent = "agent_plan", Confidence = 1 },
            summaries.Count > 0 ? "tool_ok" : "tool_error",
            finalSummary,
            string.IsNullOrWhiteSpace(finalSummary)
                ? "已达到编排步数上限，请把问题说得更具体一些。"
                : finalSummary,
            permissionDenied,
            needsApproval);
    }

    private async Task<(
        IntentResult Intent,
        string Phase,
        string? ToolSummary,
        string Fallback,
        bool PermissionDenied,
        bool NeedsApproval)> RunLegacyIntentPathAsync(
        AgentSession session,
        AgentChatRequest request,
        UserContext user,
        List<AgentToolCallLogDto> toolCalls,
        CancellationToken ct)
    {
        var intent = await intentRouter.RouteAsync(session, request.Message, ct);
        var permissionDenied = false;
        var needsApproval = false;

        var threshold = llmRuntime.ConfidenceThreshold;
        var lowConfidence = intent.Confidence > 0 && intent.Confidence < threshold;
        var isChitchat = string.Equals(intent.Intent, "chitchat", StringComparison.OrdinalIgnoreCase);
        var isUnknown = string.Equals(intent.Intent, AgentIntents.Unknown, StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrWhiteSpace(intent.Intent);

        string phase;
        string? toolSummary = null;
        string fallback;

        if (isUnknown)
        {
            phase = "unknown";
            fallback = intent.ReplyHint
                       ?? "我可以帮你查库存、查订单履约、看低库存预警。请用业务问题描述。";
        }
        else if (isChitchat)
        {
            phase = "chitchat";
            fallback = intent.ReplyHint
                       ?? "我是进销存/履约助手，可以帮你查库存、订单状态或低库存预警。";
        }
        else if (lowConfidence || intent.NeedsClarification)
        {
            phase = "clarify";
            fallback = !string.IsNullOrWhiteSpace(intent.ReplyHint)
                ? intent.ReplyHint!
                : BuildClarificationFallback(intent);
        }
        else
        {
            var cap = capabilities.FindByIntent(intent.Intent);
            if (cap is not null && !AgentSurfacePolicy.Allow(cap, user, permissionPolicy))
            {
                phase = "denied";
                fallback = "当前身份不能使用该接口。运营后台仅可查看，小程序只能操作自己的任务。";
                return (intent, phase, toolSummary, fallback, true, needsApproval);
            }

            var tool = tools.FindByIntent(intent.Intent);
            var requiredPerms = cap?.ResolvePermissions()
                ?? (string.IsNullOrWhiteSpace(tool?.RequiredPermission)
                    ? []
                    : new[] { tool!.RequiredPermission });
            var requiredPermissionLabel = requiredPerms.Count == 0
                ? string.Empty
                : string.Join(" + ", requiredPerms);

            if (tool is null)
            {
                phase = "no_tool";
                fallback = $"已识别你的需求，但尚未配置对应能力（{intent.Intent}）。";
            }
            else if (!permissionPolicy.IsAllowedAll(user, requiredPerms))
            {
                permissionDenied = true;
                phase = "denied";
                fallback = $"权限不足：需要 `{requiredPermissionLabel}`。";
            }
            else
            {
                var confirmRequired = cap?.ConfirmRequired == true
                    || (tool is HttpCapabilityTool httpTool && httpTool.ConfirmRequired);
                var approval = approvalGate.Evaluate(confirmRequired, user);
                if (!approval.Allowed)
                {
                    needsApproval = true;
                    phase = "approval";
                    fallback = approval.Message ?? "该操作需要确认后再执行。";
                }
                else
                {
                    var exec = await tool.ExecuteAsync(session, intent, ct);
                    toolCalls.Add(new AgentToolCallLogDto(exec.ToolName, exec.Success, exec.Summary, exec.ElapsedMs));
                    AgentTelemetry.RecordTool(exec.ToolName, exec.Success, exec.ElapsedMs, exec.HttpStatusCode);
                    toolSummary = exec.Summary;
                    if (exec.Success)
                    {
                        phase = "tool_ok";
                        fallback = exec.Summary;
                    }
                    else
                    {
                        phase = "tool_error";
                        fallback = $"查询失败：{exec.Error ?? "unknown"}";
                    }
                }
            }
        }

        return (intent, phase, toolSummary, fallback, permissionDenied, needsApproval);
    }

    private static bool TryFinishFromSuccessfulObservations(
        IReadOnlyList<AgentPlanObservation> observations,
        List<string> summaries,
        out AgentPlanDecision decision)
    {
        decision = null!;
        if (!observations.Any(o => o.Success))
        {
            return false;
        }

        var fact = JoinSummaries(summaries);
        if (string.IsNullOrWhiteSpace(fact))
        {
            fact = observations.Last(o => o.Success).Summary;
        }

        if (string.IsNullOrWhiteSpace(fact))
        {
            return false;
        }

        decision = new AgentPlanDecision
        {
            Action = "finish",
            Reason = fact
        };
        return true;
    }

    private static UserContext WithConfirmed(UserContext user, bool confirmed) =>
        new()
        {
            UserId = user.UserId,
            TenantId = user.TenantId,
            DisplayName = user.DisplayName,
            Roles = user.Roles,
            PermissionCodes = user.PermissionCodes,
            AccessToken = user.AccessToken,
            Channel = user.Channel,
            Confirmed = confirmed,
            TraceId = user.TraceId
        };

    private static string? JoinSummaries(List<string> summaries) =>
        summaries.Count == 0 ? null : string.Join(" | ", summaries);

    private static void ApplyTaskPlanToSession(AgentSession session, AgentTaskPlan plan)
    {
        session.Slots["_task_intent"] = plan.Intent;
        session.Slots["_task_reason"] = plan.Reason ?? string.Empty;
        if (plan.SuggestedSteps.Count > 0)
        {
            session.Slots["_task_steps"] = string.Join(",", plan.SuggestedSteps);
        }

        foreach (var (k, v) in plan.Entities)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                session.Slots[$"_ent_{k}"] = v.Trim();
                // 常用实体提升到执行槽位
                if (string.Equals(k, "product", StringComparison.OrdinalIgnoreCase))
                {
                    session.Slots["keywords"] = v.Trim();
                }
                else if (string.Equals(k, "minStock", StringComparison.OrdinalIgnoreCase))
                {
                    session.Slots["minStock"] = v.Trim();
                }
                else if (string.Equals(k, "purchaseQuantity", StringComparison.OrdinalIgnoreCase))
                {
                    session.Slots["purchaseQuantity"] = v.Trim();
                }
            }
        }
    }

    private static void ApplyProductResolveToSession(AgentSession session, ProductResolveResult resolved)
    {
        if (!string.IsNullOrWhiteSpace(resolved.Name))
        {
            session.Slots["keywords"] = resolved.Name!;
            session.Slots["_resolved_product"] = resolved.Name!;
        }

        if (resolved.ProductId is > 0)
        {
            session.Slots["productId"] = resolved.ProductId.Value.ToString();
        }

        if (resolved.MaterialId is > 0)
        {
            session.Slots["materialId"] = resolved.MaterialId.Value.ToString();
        }

        if (resolved.BaseUomId is > 0)
        {
            session.Slots["uomId"] = resolved.BaseUomId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(resolved.Sku))
        {
            session.Slots["sku"] = resolved.Sku!;
        }
    }

    /// <summary>
    /// 清洗品名关键词：去掉「库存」后半段条件句，避免复合指令污染主档检索。
    /// </summary>
    private static string SanitizeProductKeywords(string raw)
    {
        var cleaned = ProductNameNormalizer.Clean(raw);
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            return cleaned;
        }

        return ProductNameNormalizer.Extract(raw) ?? string.Empty;
    }

    private static bool TryGetAuthTerminal(
        IReadOnlyList<AgentPlanObservation> observations,
        out string phase,
        out string fallback)
    {
        phase = "auth_failed";
        fallback = string.Empty;
        var authHit = observations.LastOrDefault(IsAuthFailure);
        if (authHit is null)
        {
            return false;
        }

        fallback =
            "下游接口鉴权失败（HTTP 401/403），未能查询到真实业务数据。"
            + "请在请求中携带有效 AccessToken，或配置 Agent:HostApi / AuthProfiles 凭证后重试。"
            + "本次不会返回任何库存/订单数字。";
        if (!string.IsNullOrWhiteSpace(authHit.Error))
        {
            fallback += $"（详情：{authHit.Error}）";
        }

        return true;
    }

    private static bool IsAuthFailure(AgentPlanObservation o) =>
        !o.Success
        && (string.Equals(o.ErrorKind, "auth", StringComparison.OrdinalIgnoreCase)
            || o.HttpStatusCode is 401 or 403
            || (o.Error?.Contains("HTTP 401", StringComparison.OrdinalIgnoreCase) ?? false)
            || (o.Error?.Contains("HTTP 403", StringComparison.OrdinalIgnoreCase) ?? false)
            || (o.Error?.Contains("鉴权", StringComparison.Ordinal) ?? false)
            || (o.Summary?.Contains("鉴权失败", StringComparison.Ordinal) ?? false)
            || (o.Summary?.Contains("HTTP 401", StringComparison.OrdinalIgnoreCase) ?? false));

    private static bool TryStopDeadLoop(
        IReadOnlyList<AgentPlanObservation> observations,
        AgentPlanDecision decision,
        out string phase,
        out string fallback)
    {
        phase = "tool_error";
        fallback = string.Empty;
        var tool = decision.Tool ?? string.Empty;

        // 同能力 + 同参已成功：事实已够，禁止再查
        var sameArgsOk = observations.LastOrDefault(o =>
            o.Success
            && string.Equals(o.Tool, tool, StringComparison.OrdinalIgnoreCase)
            && SameArguments(o.Arguments, decision.Arguments));

        if (sameArgsOk is not null)
        {
            phase = "already_answered";
            fallback = string.IsNullOrWhiteSpace(sameArgsOk.Summary)
                ? $"已查询 `{tool}`，请基于已有结果回答用户。"
                : sameArgsOk.Summary;
            return true;
        }

        var authHit = observations.LastOrDefault(o =>
            string.Equals(o.Tool, tool, StringComparison.OrdinalIgnoreCase)
            && IsAuthFailure(o));

        if (authHit is not null)
        {
            phase = "auth_failed";
            fallback =
                "下游接口鉴权失败（HTTP 401/403），未能查询到真实业务数据。"
                + "请检查 AccessToken / 权限配置后重试，不要重复同一查询。";
            return true;
        }

        var sameArgsFail = observations.LastOrDefault(o =>
            !o.Success
            && string.Equals(o.Tool, tool, StringComparison.OrdinalIgnoreCase)
            && SameArguments(o.Arguments, decision.Arguments));

        if (sameArgsFail is not null)
        {
            phase = "retry_blocked";
            fallback =
                $"能力 `{tool}` 已用相同参数失败（{sameArgsFail.Error ?? sameArgsFail.Summary}）。请换参数、澄清需求或结束，禁止同参重试。";
            return true;
        }

        // Stub 假成功也禁止同参再刷（历史兜底；正常路径 4xx 已不再 stub）
        var stubHit = observations.LastOrDefault(o =>
            string.Equals(o.Tool, tool, StringComparison.OrdinalIgnoreCase)
            && SameArguments(o.Arguments, decision.Arguments)
            && (o.Summary.Contains("已回退 stub", StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.ErrorKind, "stub", StringComparison.OrdinalIgnoreCase)));

        if (stubHit is not null)
        {
            phase = "stub_blocked";
            fallback =
                $"能力 `{tool}` 上次为 stub 回退结果，不能当作真实业务数据反复调用。请修复下游 API 或凭证后再查。";
            return true;
        }

        return false;
    }

    private static bool SameArguments(
        Dictionary<string, string>? a,
        Dictionary<string, string>? b)
    {
        a ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        b ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (k, v) in a)
        {
            if (!b.TryGetValue(k, out var bv)
                || !string.Equals((v ?? "").Trim(), (bv ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string? ClassifyError(string? error, int? httpStatus)
    {
        if (httpStatus is 401 or 403)
        {
            return "auth";
        }

        if (httpStatus is > 0)
        {
            return "http";
        }

        var err = error ?? string.Empty;
        if (err.Contains("HTTP 401", StringComparison.OrdinalIgnoreCase)
            || err.Contains("HTTP 403", StringComparison.OrdinalIgnoreCase)
            || err.Contains("鉴权", StringComparison.Ordinal))
        {
            return "auth";
        }

        return string.IsNullOrWhiteSpace(err) ? null : "error";
    }

    private static string ExtractPreview(object? data)
    {
        if (data is null)
        {
            return string.Empty;
        }

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            return json.Length <= 1500 ? json : json[..1500] + "...";
        }
        catch
        {
            return data.ToString() ?? string.Empty;
        }
    }

    private static string BuildClarificationFallback(IntentResult intent) =>
        intent.MissingSlots.Count > 0
            ? $"还需要补充：{string.Join("、", intent.MissingSlots)}"
            : "请再具体一些，例如品名、SKU 或订单号。";
}
