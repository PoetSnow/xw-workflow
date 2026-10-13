namespace XW.Agent.Application.Services;

/// <summary>
/// 保存会话时，本轮改过的槽位和库里的新值不一致。
/// 调用方应把说明返回给用户，而不是用后写入的槽位盖掉先写入的。
/// </summary>
public sealed class SessionConcurrencyException : InvalidOperationException
{
    public SessionConcurrencyException(string? conflictKey)
        : base(string.IsNullOrWhiteSpace(conflictKey)
            ? "会话版本冲突，未覆盖另一轮写入。"
            : $"会话槽位冲突：{conflictKey}")
    {
        ConflictKey = conflictKey;
    }

    /// <summary>发生分歧的槽位名；版本号本身冲突时为空。</summary>
    public string? ConflictKey { get; }
}
