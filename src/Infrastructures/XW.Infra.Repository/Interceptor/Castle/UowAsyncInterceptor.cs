using Castle.DynamicProxy;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using XW.Infra.Repository;
using XW.Shared.Interception;

namespace XW.Infra.Repository.Interceptor.Castle;

/// <summary>
/// Castle 异步拦截器：为标注 <see cref="UnitOfWorkAttribute"/> 的 Application Service 方法包裹事务。
/// <para>
/// 支持两种路径：
/// <list type="number">
/// <item><description><see cref="UnitOfWorkAttribute.ModuleScope"/> → <see cref="IModuleUnitOfWork"/>（跨模块）。</description></item>
/// <item><description>否则 → <see cref="IUnitOfWork"/>（单 DbContext，如 SupplyChain 内部过账）。</description></item>
/// </list>
/// 若模块事务已处于 Active 状态，嵌套调用直接执行业务方法，不再重复 Begin/Commit。
/// </para>
/// </summary>
/// <param name="serviceProvider">用于按路径解析 <see cref="IUnitOfWork"/> 或 <see cref="IModuleUnitOfWork"/>。</param>
public class UowAsyncInterceptor(IServiceProvider serviceProvider) : IAsyncInterceptor
{
    /// <summary>
    /// 同步方法拦截入口。无 <see cref="UnitOfWorkAttribute"/> 时直接放行。
    /// </summary>
    /// <param name="invocation">Castle 调用上下文。</param>
    public void InterceptSynchronous(IInvocation invocation)
    {
        var attribute = GetAttribute(invocation);

        if (attribute == null)
        {
            invocation.Proceed();
        }
        else
        {
            InternalInterceptSynchronous(invocation, attribute);
        }
    }

    /// <summary>
    /// 无返回值的异步方法拦截入口。
    /// </summary>
    /// <param name="invocation">Castle 调用上下文。</param>
    public void InterceptAsynchronous(IInvocation invocation)
    {
        var attribute = GetAttribute(invocation);

        invocation.ReturnValue = (attribute is null)
            ? InternalInterceptAsynchronousWithoutUow(invocation)
            : InternalInterceptAsynchronous(invocation, attribute);
    }

    /// <summary>
    /// 有返回值的异步方法拦截入口。
    /// </summary>
    /// <typeparam name="TResult">被拦截方法的返回类型。</typeparam>
    /// <param name="invocation">Castle 调用上下文。</param>
    public void InterceptAsynchronous<TResult>(IInvocation invocation)
    {
        var attribute = GetAttribute(invocation);

        invocation.ReturnValue = (attribute is null)
            ? InternalInterceptAsynchronousWithoutUow<TResult>(invocation)
            : InternalInterceptAsynchronous<TResult>(invocation, attribute);
    }

    /// <summary>
    /// 同步 + 单 DbContext 事务。模块事务（<see cref="UnitOfWorkAttribute.ModuleScope"/>）不支持同步路径。
    /// </summary>
    /// <param name="invocation">Castle 调用上下文。</param>
    /// <param name="attribute">单元工作特性。</param>
    private void InternalInterceptSynchronous(IInvocation invocation, UnitOfWorkAttribute attribute)
    {
        if (UsesModuleScope(attribute))
        {
            throw new NotSupportedException("模块事务不支持同步拦截，请使用 async 方法。");
        }

        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        if (unitOfWork.IsStartingUow)
        {
            invocation.Proceed();
            return;
        }

        try
        {
            unitOfWork.BeginTransaction(distributed: attribute.Distributed);
            invocation.Proceed();
            unitOfWork.Commit();
        }
        catch (Exception)
        {
            unitOfWork.Rollback();
            throw;
        }
        finally
        {
            unitOfWork.Dispose();
        }
    }

    /// <summary>
    /// 无返回值的异步事务包裹：优先检测已 Active 的模块事务，再按 ModuleScope 或 IUnitOfWork 分支。
    /// </summary>
    /// <param name="invocation">Castle 调用上下文。</param>
    /// <param name="attribute">单元工作特性。</param>
    private async Task InternalInterceptAsynchronous(IInvocation invocation, UnitOfWorkAttribute attribute)
    {
        if (TryGetActiveModuleUnitOfWork(out _))
        {
            invocation.Proceed();
            await (Task)invocation.ReturnValue;
            return;
        }

        if (UsesModuleScope(attribute))
        {
            var moduleUnitOfWork = serviceProvider.GetRequiredService<IModuleUnitOfWork>();
            try
            {
                await moduleUnitOfWork.BeginAsync(attribute.ModuleScope);

                invocation.Proceed();
                var task = (Task)invocation.ReturnValue;
                await task;

                await moduleUnitOfWork.CommitAsync();
            }
            catch (Exception)
            {
                await moduleUnitOfWork.RollbackAsync();
                throw;
            }
            finally
            {
                await moduleUnitOfWork.DisposeAsync();
            }

            return;
        }

        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        if (unitOfWork.IsStartingUow)
        {
            invocation.Proceed();
            await (Task)invocation.ReturnValue;
            return;
        }

        try
        {
            unitOfWork.BeginTransaction(distributed: attribute.Distributed);

            invocation.Proceed();
            var task = (Task)invocation.ReturnValue;
            await task;

            await unitOfWork.CommitAsync();
        }
        catch (Exception)
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
        finally
        {
            unitOfWork.Dispose();
        }
    }

    /// <summary>
    /// 有返回值的异步事务包裹，逻辑同 <see cref="InternalInterceptAsynchronous"/>。
    /// </summary>
    /// <typeparam name="TResult">被拦截方法的返回类型。</typeparam>
    /// <param name="invocation">Castle 调用上下文。</param>
    /// <param name="attribute">单元工作特性。</param>
    /// <returns>业务方法的返回值。</returns>
    private async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation, UnitOfWorkAttribute attribute)
    {
        if (TryGetActiveModuleUnitOfWork(out _))
        {
            invocation.Proceed();
            var nestedTask = (Task<TResult>)invocation.ReturnValue;
            return await nestedTask;
        }

        if (UsesModuleScope(attribute))
        {
            var moduleUnitOfWork = serviceProvider.GetRequiredService<IModuleUnitOfWork>();
            try
            {
                await moduleUnitOfWork.BeginAsync(attribute.ModuleScope);

                invocation.Proceed();
                var moduleTask = (Task<TResult>)invocation.ReturnValue;
                var moduleResult = await moduleTask;

                await moduleUnitOfWork.CommitAsync();
                return moduleResult;
            }
            catch (Exception)
            {
                await moduleUnitOfWork.RollbackAsync();
                throw;
            }
            finally
            {
                await moduleUnitOfWork.DisposeAsync();
            }
        }

        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        if (unitOfWork.IsStartingUow)
        {
            invocation.Proceed();
            return await (Task<TResult>)invocation.ReturnValue;
        }

        TResult result;

        try
        {
            unitOfWork.BeginTransaction(distributed: attribute.Distributed);

            invocation.Proceed();
            var task = (Task<TResult>)invocation.ReturnValue;
            result = await task;

            await unitOfWork.CommitAsync();
        }
        catch (Exception)
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
        finally
        {
            unitOfWork.Dispose();
        }

        return result;
    }

    /// <summary>
    /// 无事务、无返回值：直接 await 业务 Task。
    /// </summary>
    /// <param name="invocation">Castle 调用上下文。</param>
    private static async Task InternalInterceptAsynchronousWithoutUow(IInvocation invocation)
    {
        invocation.Proceed();
        var task = (Task)invocation.ReturnValue;
        await task;
    }

    /// <summary>
    /// 无事务、有返回值：直接 await 并返回业务结果。
    /// </summary>
    /// <typeparam name="TResult">被拦截方法的返回类型。</typeparam>
    /// <param name="invocation">Castle 调用上下文。</param>
    /// <returns>业务方法的返回值。</returns>
    private static async Task<TResult> InternalInterceptAsynchronousWithoutUow<TResult>(IInvocation invocation)
    {
        invocation.Proceed();
        var task = (Task<TResult>)invocation.ReturnValue;
        return await task;
    }

    /// <summary>
    /// 检测当前作用域是否已有进行中的模块事务（用于嵌套 UoW 场景）。
    /// </summary>
    /// <param name="moduleUnitOfWork">解析出的模块 UoW 实例。</param>
    /// <returns>若 <see cref="IModuleUnitOfWork.IsActive"/> 为 true 则返回 true。</returns>
    private bool TryGetActiveModuleUnitOfWork(out IModuleUnitOfWork moduleUnitOfWork)
    {
        moduleUnitOfWork = serviceProvider.GetRequiredService<IModuleUnitOfWork>();
        return moduleUnitOfWork.IsActive;
    }

    /// <summary>
    /// 判断特性是否要求跨模块事务（而非单 DbContext UoW）。
    /// </summary>
    /// <param name="attribute">单元工作特性。</param>
    private static bool UsesModuleScope(UnitOfWorkAttribute attribute) =>
        attribute.ModuleScope is not ModuleUnitOfWorkScope.None;

    /// <summary>
    /// 从被拦截方法读取 <see cref="UnitOfWorkAttribute"/>（接口方法或实现类方法均可）。
    /// </summary>
    /// <param name="invocation">Castle 调用上下文。</param>
    /// <returns>特性实例；未标注时返回 null。</returns>
    private static UnitOfWorkAttribute? GetAttribute(IInvocation invocation)
    {
        var methodInfo = invocation.Method ?? invocation.MethodInvocationTarget;
        var attribute = methodInfo.GetCustomAttribute<UnitOfWorkAttribute>();
        return attribute;
    }
}
