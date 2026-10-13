namespace XW.Shared.Interception;

/// <summary>标记方法执行后清除缓存。</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class CachingEvictAttribute : CachingInterceptorAttribute
{
    /// <summary>要清除的缓存键。</summary>
    public string[] CacheKeys { get; set; } = [];
}
