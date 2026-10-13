using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Contracts;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Runtime;

/// <summary>线性工作流一次执行的结果。</summary>
public sealed class WorkflowExecutionResult
{
    /// <summary>全部步骤是否成功。</summary>
    public bool Success { get; init; }

    /// <summary>步骤摘要串联。</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>失败错误码/原因。</summary>
    public string? Error { get; init; }

    /// <summary>空结果时的追问文案（OnEmpty=clarify）。</summary>
    public string? ClarifyQuestion { get; init; }

    /// <summary>总耗时毫秒。</summary>
    public long ElapsedMs { get; init; }

    /// <summary>各步骤工具调用日志。</summary>
    public List<AgentToolCallLogDto> StepLogs { get; init; } = [];

    /// <summary>失败步骤的下游 HTTP 状态码（如 401）。</summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>错误分类：auth | http | assign | empty | …</summary>
    public string? ErrorKind { get; init; }
}

/// <summary>工作流执行器：按声明式步骤顺序调用 Tool。</summary>
public interface IWorkflowExecutor
{
    /// <summary>执行工作流；任一步失败即整段失败并停止。</summary>
    Task<WorkflowExecutionResult> ExecuteAsync(
        AgentWorkflowDefinition workflow,
        AgentSession session,
        IReadOnlyDictionary<string, string> inputArgs,
        CancellationToken ct = default);
}

/// <summary>线性 Workflow：按步骤调 Tool，做简单变量赋值。非 BPM。</summary>
public sealed class LinearWorkflowExecutor(
    IToolRegistry tools,
    ILogger<LinearWorkflowExecutor> logger) : IWorkflowExecutor
{
    /// <inheritdoc />
    public async Task<WorkflowExecutionResult> ExecuteAsync(
        AgentWorkflowDefinition workflow,
        AgentSession session,
        IReadOnlyDictionary<string, string> inputArgs,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in inputArgs)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                vars[k] = v.Trim();
            }
        }

        foreach (var (k, v) in session.Slots)
        {
            vars.TryAdd(k, v);
        }

        var stepLogs = new List<AgentToolCallLogDto>();
        var summaries = new List<string>();

        foreach (var step in workflow.Steps)
        {
            if (ShouldSkipStep(step, vars))
            {
                logger.LogInformation(
                    "Workflow {Wf} step {Step} skipped (var present: {Skip})",
                    workflow.Code, step.Id, step.SkipIfVarPresent);
                continue;
            }

            var tool = tools.FindByCode(step.Tool);
            if (tool is null)
            {
                return Fail(sw, stepLogs, $"workflow step {step.Id}: tool {step.Tool} not found");
            }

            var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (ak, template) in step.Arguments)
            {
                if (TryOmitOptionalArgument(template, vars, out var omitted) && omitted)
                {
                    continue;
                }

                var rendered = Render(template, vars);
                if (ContainsUnresolvedPlaceholder(rendered))
                {
                    return Fail(sw, stepLogs,
                        $"workflow step {step.Id}: argument {ak} unresolved: {rendered}");
                }

                if (!string.IsNullOrWhiteSpace(rendered))
                {
                    args[ak] = rendered;
                }
            }

            foreach (var (ak, av) in args)
            {
                session.Slots[ak] = av;
            }

            var intent = new IntentResult
            {
                Intent = step.Tool,
                Confidence = 1,
                Slots = args
            };

            logger.LogInformation(
                "Workflow {Wf} step {Step} tool={Tool} args=[{Args}]",
                workflow.Code, step.Id, step.Tool, string.Join(",", args.Select(kv => $"{kv.Key}={kv.Value}")));

            var exec = await tool.ExecuteAsync(session, intent, ct);
            stepLogs.Add(new AgentToolCallLogDto(exec.ToolName, exec.Success, exec.Summary, exec.ElapsedMs));

            if (!exec.Success)
            {
                // 任一步失败：整段 workflow 失败并立即停止，不进入后续步骤（如 s2）
                var kind = ClassifyToolError(exec);
                return new WorkflowExecutionResult
                {
                    Success = false,
                    Error = exec.Error ?? "step_failed",
                    ErrorKind = kind,
                    HttpStatusCode = exec.HttpStatusCode,
                    Summary = $"工作流 {workflow.Code} 在步骤 {step.Id} 失败：{exec.Summary}",
                    ElapsedMs = sw.ElapsedMilliseconds,
                    StepLogs = stepLogs
                };
            }

            summaries.Add(exec.Summary);

            // 库存账有行：汇总 qtyOnHand 写入 session（空账在 OnEmpty=ok 分支写 0）
            if (IsInventoryBalancesStep(step)
                && !IsEmptyResult(exec)
                && TrySumQtyOnHand(exec.Data, out var onHandSum))
            {
                session.Slots["qtyOnHand"] = onHandSum.ToString("0.##", CultureInfo.InvariantCulture);
                session.Slots.Remove("_inventory_empty");
            }

            // 先判空：OnEmpty=clarify 时不要再绑 {{firstId}}（否则会变成 assign 失败）
            if (IsEmptyResult(exec))
            {
                if (string.Equals(step.OnEmpty, "continue", StringComparison.OrdinalIgnoreCase))
                {
                    // 空结果继续：跳过 Assign，进入下一步（如产品无命中 → 搜物料）
                    logger.LogInformation(
                        "Workflow {Wf} step {Step} empty → continue", workflow.Code, step.Id);
                    continue;
                }

                if (IsOnEmptyOk(step.OnEmpty))
                {
                    // 空列表是合法业务结果（库存账无行 → 现存 0）
                    var zeroSummary = BuildEmptyAsZeroSummary(step, vars, exec.Summary);
                    summaries[^1] = zeroSummary;
                    session.Slots["qtyOnHand"] = "0";
                    session.Slots["_inventory_empty"] = "1";
                    logger.LogInformation(
                        "Workflow {Wf} step {Step} empty → ok (treat as zero)", workflow.Code, step.Id);
                    continue;
                }

                if (string.Equals(step.OnEmpty, "clarify", StringComparison.OrdinalIgnoreCase))
                {
                    var hint = BuildEmptyClarify(step, vars);
                    logger.LogInformation(
                        "Workflow {Wf} step {Step} empty → clarify", workflow.Code, step.Id);
                    return new WorkflowExecutionResult
                    {
                        Success = false,
                        ClarifyQuestion = hint,
                        Error = "empty_result",
                        ErrorKind = "empty",
                        Summary = exec.Summary,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        StepLogs = stepLogs
                    };
                }

                return new WorkflowExecutionResult
                {
                    Success = false,
                    Error = "empty_result",
                    ErrorKind = "empty",
                    Summary = $"工作流 {workflow.Code} 在步骤 {step.Id} 无结果：{exec.Summary}",
                    ElapsedMs = sw.ElapsedMilliseconds,
                    StepLogs = stepLogs
                };
            }

            if (!TryApplyAssign(step, exec, vars, session, out var assignError))
            {
                // Assign 缺 firstId：若本步 OnEmpty=clarify，按空结果处理（兜底）
                if (string.Equals(step.OnEmpty, "clarify", StringComparison.OrdinalIgnoreCase)
                    && (assignError?.Contains("firstId", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    return new WorkflowExecutionResult
                    {
                        Success = false,
                        ClarifyQuestion = BuildEmptyClarify(step, vars),
                        Error = "empty_result",
                        ErrorKind = "empty",
                        Summary = exec.Summary,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        StepLogs = stepLogs
                    };
                }

                return new WorkflowExecutionResult
                {
                    Success = false,
                    Error = assignError ?? "assign_failed",
                    ErrorKind = "assign",
                    Summary = $"工作流 {workflow.Code} 在步骤 {step.Id} 变量赋值失败：{assignError}",
                    ElapsedMs = sw.ElapsedMilliseconds,
                    StepLogs = stepLogs
                };
            }
        }

        sw.Stop();
        return new WorkflowExecutionResult
        {
            Success = true,
            Summary = string.Join(" → ", summaries),
            ElapsedMs = sw.ElapsedMilliseconds,
            StepLogs = stepLogs
        };
    }

    private static bool TryApplyAssign(
        AgentWorkflowStep step,
        ToolExecutionResult exec,
        Dictionary<string, string> vars,
        AgentSession session,
        out string? error)
    {
        error = null;
        // 约定：优先从 HTTP body/preview JSON 抽 firstId，再按 Assign 写入 vars
        var firstId = TryExtractFirstId(exec.Data);
        if (!string.IsNullOrWhiteSpace(firstId))
        {
            vars["firstId"] = firstId!;
        }

        foreach (var (name, template) in step.Assign)
        {
            var value = template.Trim();
            if (value.Contains("{{firstId}}", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(firstId))
                {
                    error = $"无法解析 {{{{firstId}}}} 以赋值 {name}（上游结果无可用 id）";
                    return false;
                }

                value = value.Replace("{{firstId}}", firstId, StringComparison.OrdinalIgnoreCase);
            }
            else if (value.Contains("{{", StringComparison.Ordinal))
            {
                value = Render(value, vars);
            }

            if (ContainsUnresolvedPlaceholder(value))
            {
                error = $"步骤赋值 {name} 仍含未解析占位符：{value}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(value) && firstId is not null
                && (name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || name.Contains("product", StringComparison.OrdinalIgnoreCase)))
            {
                value = firstId;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"步骤赋值 {name} 为空";
                return false;
            }

            vars[name] = value;
            session.Slots[name] = value;
        }

        // 无 Assign 时：检索类工具自动写入对应 Id
        if (step.Assign.Count == 0 && firstId is not null)
        {
            if (step.Tool.Contains("products.search", StringComparison.OrdinalIgnoreCase))
            {
                vars["productId"] = firstId;
                session.Slots["productId"] = firstId;
            }
            else if (step.Tool.Contains("materials.search", StringComparison.OrdinalIgnoreCase))
            {
                vars["materialId"] = firstId;
                session.Slots["materialId"] = firstId;
            }
        }

        return true;
    }

    private static bool ShouldSkipStep(AgentWorkflowStep step, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrWhiteSpace(step.SkipIfVarPresent))
        {
            return false;
        }

        foreach (var part in step.SkipIfVarPresent.Split([',', '|', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var key = part.Trim();
            if (key.Length > 0
                && vars.TryGetValue(key, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 参数模板为单个占位符且变量缺失/空时，视为可选参数并省略（避免 {{productId}} 在仅物料场景失败）。
    /// </summary>
    private static bool TryOmitOptionalArgument(
        string template,
        IReadOnlyDictionary<string, string> vars,
        out bool omit)
    {
        omit = false;
        var t = template.Trim();
        if (!t.StartsWith("{{", StringComparison.Ordinal) || !t.EndsWith("}}", StringComparison.Ordinal))
        {
            return false;
        }

        var inner = t[2..^2].Trim();
        if (inner.StartsWith("input.", StringComparison.OrdinalIgnoreCase))
        {
            inner = inner[6..];
        }
        else if (inner.StartsWith("vars.", StringComparison.OrdinalIgnoreCase))
        {
            inner = inner[5..];
        }

        if (inner.Contains('{', StringComparison.Ordinal) || inner.Contains('}', StringComparison.Ordinal))
        {
            return false;
        }

        if (!vars.TryGetValue(inner, out var value) || string.IsNullOrWhiteSpace(value))
        {
            omit = true;
            return true;
        }

        return false;
    }

    private static bool ContainsUnresolvedPlaceholder(string value) =>
        value.Contains("{{", StringComparison.Ordinal) && value.Contains("}}", StringComparison.Ordinal);

    private static string? TryExtractFirstId(object? data)
    {
        if (data is null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var direct = FindFirstId(root);
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            // HttpCapabilityTool 把下游 JSON 放在 body/preview 字符串里
            foreach (var prop in new[] { "body", "preview" })
            {
                if (!root.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var raw = el.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                try
                {
                    using var inner = JsonDocument.Parse(raw);
                    var id = FindFirstId(inner.RootElement);
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }
                }
                catch
                {
                    // body 可能被 Truncate 截断，忽略
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindFirstId(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("id", out var id) && id.ValueKind != JsonValueKind.Null)
            {
                return id.ToString();
            }

            if (el.TryGetProperty("Id", out var id2) && id2.ValueKind != JsonValueKind.Null)
            {
                return id2.ToString();
            }

            if (el.TryGetProperty("items", out var items))
            {
                return FindFirstId(items);
            }

            if (el.TryGetProperty("data", out var data))
            {
                return FindFirstId(data);
            }

            if (el.TryGetProperty("list", out var list))
            {
                return FindFirstId(list);
            }
        }

        if (el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0)
        {
            return FindFirstId(el[0]);
        }

        return null;
    }

    private static string BuildEmptyClarify(AgentWorkflowStep step, IReadOnlyDictionary<string, string> vars)
    {
        vars.TryGetValue("keywords", out var kw);
        if (string.IsNullOrWhiteSpace(kw))
        {
            vars.TryGetValue("keyword", out kw);
        }

        if (!string.IsNullOrWhiteSpace(kw))
        {
            return $"没有找到与「{kw.Trim()}」匹配的产品，请确认型号/SKU 是否正确，或换一个品名再查。";
        }

        return $"未找到可用结果（步骤 {step.Id}/{step.Tool}），请补充更明确的品名或 SKU。";
    }

    private static bool IsOnEmptyOk(string? onEmpty) =>
        string.Equals(onEmpty, "ok", StringComparison.OrdinalIgnoreCase)
        || string.Equals(onEmpty, "succeed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(onEmpty, "zero", StringComparison.OrdinalIgnoreCase);

    private static bool IsInventoryBalancesStep(AgentWorkflowStep step) =>
        string.Equals(step.Tool, "inventory.balances.query", StringComparison.OrdinalIgnoreCase);

    private static bool TrySumQtyOnHand(object? data, out decimal sum)
    {
        sum = 0m;
        if (data is null)
        {
            return false;
        }

        try
        {
            var json = JsonSerializer.Serialize(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (TrySumQtyOnHandElement(root, out sum))
            {
                return true;
            }

            foreach (var prop in new[] { "body", "preview" })
            {
                if (!root.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var raw = el.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                try
                {
                    using var inner = JsonDocument.Parse(raw);
                    if (TrySumQtyOnHandElement(inner.RootElement, out sum))
                    {
                        return true;
                    }
                }
                catch
                {
                    // body 可能被截断
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TrySumQtyOnHandElement(JsonElement el, out decimal sum)
    {
        sum = 0m;
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("data", out var data))
            {
                el = data;
            }
        }

        JsonElement list = default;
        var hasList = false;
        if (el.ValueKind == JsonValueKind.Array)
        {
            list = el;
            hasList = true;
        }
        else if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "list", "items", "data", "rows" })
            {
                if (el.TryGetProperty(key, out var nested) && nested.ValueKind == JsonValueKind.Array)
                {
                    list = nested;
                    hasList = true;
                    break;
                }
            }
        }

        if (!hasList)
        {
            return false;
        }

        var found = false;
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!item.TryGetProperty("qtyOnHand", out var qh)
                && !item.TryGetProperty("QtyOnHand", out qh))
            {
                continue;
            }

            if (qh.ValueKind == JsonValueKind.Number && qh.TryGetDecimal(out var v))
            {
                sum += v;
                found = true;
            }
            else if (qh.ValueKind == JsonValueKind.String
                     && decimal.TryParse(qh.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out v))
            {
                sum += v;
                found = true;
            }
        }

        return found;
    }

    private static string BuildEmptyAsZeroSummary(
        AgentWorkflowStep step,
        IReadOnlyDictionary<string, string> vars,
        string toolSummary)
    {
        vars.TryGetValue("productId", out var productId);
        vars.TryGetValue("materialId", out var materialId);
        vars.TryGetValue("keywords", out var keywords);
        vars.TryGetValue("_resolved_product", out var name);

        var subject = !string.IsNullOrWhiteSpace(name)
            ? name!.Trim()
            : !string.IsNullOrWhiteSpace(keywords)
                ? keywords!.Trim()
                : "该商品";

        var idHint = !string.IsNullOrWhiteSpace(productId)
            ? $"productId={productId}"
            : !string.IsNullOrWhiteSpace(materialId)
                ? $"materialId={materialId}"
                : step.Tool;

        return $"{subject} 库存账无记录，按现存 0 处理（{idHint}）。{toolSummary}".Trim();
    }

    private static bool IsEmptyResult(ToolExecutionResult exec)
    {
        if (!string.IsNullOrWhiteSpace(exec.Summary))
        {
            if (exec.Summary.Contains("\"list\":[]", StringComparison.Ordinal)
                || exec.Summary.Contains("\"items\":[]", StringComparison.Ordinal)
                || exec.Summary.Contains("\"rowsCount\":0", StringComparison.Ordinal)
                || exec.Summary.Contains("\"total\":0", StringComparison.Ordinal)
                || exec.Summary.Contains("0 条", StringComparison.Ordinal)
                || exec.Summary.Contains("无结果", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (exec.Data is null)
        {
            return string.IsNullOrWhiteSpace(exec.Summary);
        }

        try
        {
            var json = JsonSerializer.Serialize(exec.Data);
            using var doc = JsonDocument.Parse(json);
            if (IsEmptyJson(doc.RootElement))
            {
                return true;
            }

            // HttpCapabilityTool：真实列表在 body/preview 字符串里
            foreach (var prop in new[] { "body", "preview" })
            {
                if (!doc.RootElement.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var raw = el.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                try
                {
                    using var inner = JsonDocument.Parse(raw);
                    if (IsEmptyJson(inner.RootElement))
                    {
                        return true;
                    }
                }
                catch
                {
                    if (raw.Contains("\"list\":[]", StringComparison.Ordinal)
                        || raw.Contains("\"rowsCount\":0", StringComparison.Ordinal)
                        || raw.Contains("\"total\":0", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static bool IsEmptyJson(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.GetArrayLength() == 0;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("rowsCount", out var rc) && rc.TryGetInt32(out var rows) && rows == 0)
        {
            return true;
        }

        if (root.TryGetProperty("total", out var total) && total.TryGetInt32(out var t) && t == 0)
        {
            // 有 list/items 时以集合为准；仅 total=0 也视为空
            if (root.TryGetProperty("list", out var list0) && list0.ValueKind == JsonValueKind.Array)
            {
                return list0.GetArrayLength() == 0;
            }

            if (root.TryGetProperty("items", out var items0) && items0.ValueKind == JsonValueKind.Array)
            {
                return items0.GetArrayLength() == 0;
            }

            return true;
        }

        if (root.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            return list.GetArrayLength() == 0;
        }

        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            return items.GetArrayLength() == 0;
        }

        if (root.TryGetProperty("data", out var data))
        {
            return IsEmptyJson(data);
        }

        return false;
    }

    private static string? ClassifyToolError(ToolExecutionResult exec)
    {
        if (exec.HttpStatusCode is 401 or 403)
        {
            return "auth";
        }

        if (exec.HttpStatusCode is > 0)
        {
            return "http";
        }

        var err = exec.Error ?? string.Empty;
        if (err.Contains("HTTP 401", StringComparison.OrdinalIgnoreCase)
            || err.Contains("HTTP 403", StringComparison.OrdinalIgnoreCase)
            || err.Contains("鉴权", StringComparison.Ordinal))
        {
            return "auth";
        }

        return string.IsNullOrWhiteSpace(err) ? null : "error";
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> vars)
    {
        var result = template;
        foreach (var (k, v) in vars)
        {
            result = result.Replace("{{" + k + "}}", v, StringComparison.OrdinalIgnoreCase);
            result = result.Replace("{{input." + k + "}}", v, StringComparison.OrdinalIgnoreCase);
            result = result.Replace("{{vars." + k + "}}", v, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static WorkflowExecutionResult Fail(
        Stopwatch sw,
        List<AgentToolCallLogDto> logs,
        string error) =>
        new()
        {
            Success = false,
            Error = error,
            Summary = error,
            ElapsedMs = sw.ElapsedMilliseconds,
            StepLogs = logs
        };
}
