namespace XW.Agent.Application.Llm;

/// <summary>
/// 规划、任务拆解、回复这三类 LLM 调用：网络错误或 429/503 时最多再试 1 次。
/// 不用于探测，也不用于工具 HTTP。采购 POST 仍由网关把非 GET 的重试次数压成 0。
/// </summary>
internal static class LlmTransientRetry
{
    public static async Task<T> OnceAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && IsTransient(ex))
        {
            // 只再试一次。第二次仍失败则把异常交给调用方原有的降级逻辑。
            return await action(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>429/503 以及 HttpRequestException 视为瞬时失败。400 等业务错误不重试。</summary>
    public static bool IsTransient(Exception ex)
    {
        if (ex is HttpRequestException)
        {
            return true;
        }

        if (ex is not InvalidOperationException)
        {
            return false;
        }

        const string prefix = "LLM call failed:";
        var message = ex.Message;
        var index = message.LastIndexOf(prefix, StringComparison.Ordinal);
        if (index < 0)
        {
            return false;
        }

        var code = message[(index + prefix.Length)..].Trim();
        return code is "429" or "503";
    }
}
