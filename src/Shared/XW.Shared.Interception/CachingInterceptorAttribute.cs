namespace XW.Shared.Interception;

/// <summary>缓存拦截特性基类。由 Redis 缓存拦截器读取。</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class CachingInterceptorAttribute : Attribute
{
    /// <summary>缓存键前缀。</summary>
    public string CacheKeyPrefix { get; set; } = string.Empty;

    /// <summary>缓存提供者出错时不影响业务。</summary>
    public bool IsHighAvailability { get; set; } = true;

    /// <summary>缓存键。</summary>
    public string CacheKey { get; set; } = string.Empty;
}
