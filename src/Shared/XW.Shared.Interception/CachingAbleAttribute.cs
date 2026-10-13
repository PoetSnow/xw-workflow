namespace XW.Shared.Interception;

/// <summary>标记方法结果可缓存。</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class CachingAbleAttribute : CachingInterceptorAttribute
{
    /// <summary>过期时间，单位秒。默认 30 秒。</summary>
    public int Expiration { get; set; } = 30;
}
