using XW.Agent.Application.Models;

namespace XW.Agent.Application.Planning;

/// <summary>
/// 第一阶段 Task Plan：一句话拆成业务意图 + 实体，再交给 Resolve / Capability 执行。
/// 不等于逐步 ReAct 的 <see cref="AgentPlanDecision"/>。
/// </summary>
public sealed class AgentTaskPlan
{
    /// <summary>业务意图码，如 inventory_purchase_check / inventory_query / purchase_create。</summary>
    public required string Intent { get; init; }

    /// <summary>抽取的实体（product / minStock / purchaseQuantity …）。</summary>
    public Dictionary<string, string> Entities { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>建议执行步骤名（第二阶段 Workflow 可直接消费；本阶段仅作提示）。</summary>
    public IReadOnlyList<string> SuggestedSteps { get; init; } = [];

    /// <summary>置信度 0~1。</summary>
    public double Confidence { get; init; } = 0.7;

    /// <summary>内部理由。</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Intent + Entity：一句话 → Task Plan。
/// 与逐步 <see cref="IAgentPlanner.NextAsync"/> 并存。
/// </summary>
public interface IAgentTaskPlanner
{
    Task<AgentTaskPlan> CreatePlanAsync(
        AgentSession session,
        string message,
        CancellationToken cancellationToken = default);
}

/// <summary>商品实体解析结果。</summary>
public sealed class ProductResolveResult
{
    public bool Success { get; init; }
    public bool NeedConfirm { get; init; }
    public long? ProductId { get; init; }
    public long? MaterialId { get; init; }
    public long? BaseUomId { get; init; }
    public string? Sku { get; init; }
    public string? Name { get; init; }
    public double Confidence { get; init; }
    public string? Question { get; init; }
    public IReadOnlyList<ProductResolveOption> Options { get; init; } = [];
    public string? Summary { get; init; }
}

/// <summary>低置信时供用户选择的候选。</summary>
public sealed class ProductResolveOption
{
    public long? ProductId { get; init; }
    public long? MaterialId { get; init; }
    public long? BaseUomId { get; init; }
    public string? Sku { get; init; }
    public string Name { get; init; } = string.Empty;
    public double Score { get; init; }
}
