using Castle.DynamicProxy;

namespace XW.Shared.Application.Interceptors;

/// <summary>
/// 操作日志拦截器（同步入口，委托给 <see cref="OperateLogAsyncInterceptor"/>）。
/// </summary>
public class OperateLogInterceptor(OperateLogAsyncInterceptor opsLogAsyncInterceptor) : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        opsLogAsyncInterceptor.ToInterceptor().Intercept(invocation);
    }
}
