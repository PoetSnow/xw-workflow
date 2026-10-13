using Castle.DynamicProxy;

namespace XW.Infra.Redis.Caching.Core.Interceptor.Castle;

/// <summary>
/// caching interceptor
/// </summary>
public class CachingInterceptor(CachingAsyncInterceptor cachingAsyncInterceptor) : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        cachingAsyncInterceptor.ToInterceptor().Intercept(invocation);
    }
}
