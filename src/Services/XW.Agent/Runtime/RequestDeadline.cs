namespace XW.Agent.Application.Runtime;

/// <summary>
/// 给单次 HTTP 动作加服务端截止时间，并与调用方的取消令牌串在一起。
/// 聊天和上传各用各的秒数，不设 Kestrel 全局超时。
/// </summary>
public sealed class RequestDeadline : IDisposable
{
    private readonly CancellationToken _caller;
    private readonly CancellationTokenSource? _owned;

    private RequestDeadline(CancellationToken caller, CancellationTokenSource? owned)
    {
        _caller = caller;
        _owned = owned;
        Token = owned?.Token ?? caller;
    }

    /// <summary>传给编排器或入库逻辑的令牌。</summary>
    public CancellationToken Token { get; }

    /// <summary>
    /// 截止时间到了，且调用方没有先取消。
    /// 调用方断开时不算服务端超时，避免把用户取消报成 deadline。
    /// </summary>
    public bool Fired =>
        _owned is not null
        && _owned.IsCancellationRequested
        && !_caller.IsCancellationRequested;

    /// <summary>seconds ≤ 0 时不另加截止，只跟随 <paramref name="caller"/>。</summary>
    public static RequestDeadline Start(CancellationToken caller, int seconds)
    {
        if (seconds <= 0)
        {
            return new RequestDeadline(caller, null);
        }

        return Start(caller, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>测试用较短间隔。生产路径走 <see cref="Start(CancellationToken, int)"/>。</summary>
    internal static RequestDeadline Start(CancellationToken caller, TimeSpan delay)
    {
        var owned = CancellationTokenSource.CreateLinkedTokenSource(caller);
        owned.CancelAfter(delay);
        return new RequestDeadline(caller, owned);
    }

    public void Dispose() => _owned?.Dispose();
}
