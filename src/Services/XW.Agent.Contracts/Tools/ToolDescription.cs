namespace XW.Agent.Contracts.Tools;

/// <summary>工具说明。规划器只看这些字段，不看执行绑定。</summary>
public sealed class ToolDescription
{
    /// <summary>工具编码。</summary>
    public required string Code { get; init; }

    /// <summary>何时使用。</summary>
    public required string WhenToUse { get; init; }

    /// <summary>参数说明。</summary>
    public string? Parameters { get; init; }

    /// <summary>所需权限。</summary>
    public string? Permission { get; init; }

    /// <summary>风险等级。</summary>
    public string? Risk { get; init; }
}
