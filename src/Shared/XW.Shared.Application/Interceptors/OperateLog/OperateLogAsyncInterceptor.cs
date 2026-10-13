using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using XW.Infra.Core.Json;
using XW.Infra.IdGenerater.Yitter;
using XW.Shared.Application.Contracts.Attributes;
using XW.Shared.Repository.DapperEntities;
using System.Reflection;
using System.Text.Json;

namespace XW.Shared.Application.Interceptors;

/// <summary>
/// 操作日志异步拦截器：识别 <see cref="OperateLogAttribute"/>，记录方法调用并写入 Channel。
/// </summary>
public sealed class OperateLogAsyncInterceptor(UserContext userContext, ILogger<OperateLogAsyncInterceptor> logger) : IAsyncInterceptor
{
    /// <summary>同步方法拦截。</summary>
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

    /// <summary>无返回值的异步方法拦截。</summary>
    public void InterceptAsynchronous(IInvocation invocation)
    {
        var attribute = GetAttribute(invocation);

        invocation.ReturnValue = attribute == null
                                 ? InternalInterceptAsynchronousWithOutOpsLog(invocation)
                                 : InternalInterceptAsynchronous(invocation, attribute)
                                 ;
    }

    /// <summary>有返回值的异步方法拦截。</summary>
    public void InterceptAsynchronous<TResult>(IInvocation invocation)
    {
        var attribute = GetAttribute(invocation);

        invocation.ReturnValue = attribute == null
                                 ? InternalInterceptAsynchronousWithOutOpsLog<TResult>(invocation)
                                 : InternalInterceptAsynchronous<TResult>(invocation, attribute)
                                 ;
    }

    private void InternalInterceptSynchronous(IInvocation invocation, OperateLogAttribute attribute)
    {
        var methodInfo = invocation.Method ?? invocation.MethodInvocationTarget;
        var fullName = methodInfo.DeclaringType?.FullName ?? string.Empty;
        var startTime = DateTime.Now;
        var log = CreateOpsLog(fullName, methodInfo.Name, attribute.LogName, invocation.Arguments, userContext);
        try
        {
            invocation.Proceed();
            log.Succeed = true;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            log.ExecutionTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
            WriteOpsLog(log);
        }
    }

    private async Task InternalInterceptAsynchronous(IInvocation invocation, OperateLogAttribute attribute)
    {
        var methodInfo = invocation.Method ?? invocation.MethodInvocationTarget;
        var fullName = methodInfo.DeclaringType?.FullName ?? string.Empty;
        var startTime = DateTime.Now;
        var log = CreateOpsLog(fullName, methodInfo.Name, attribute.LogName, invocation.Arguments, userContext);

        try
        {
            invocation.Proceed();
            var task = (Task)invocation.ReturnValue;
            await task;
            log.Succeed = true;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            log.ExecutionTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
            WriteOpsLog(log);
        }
    }

    private static async Task InternalInterceptAsynchronousWithOutOpsLog(IInvocation invocation)
    {
        invocation.Proceed();
        var task = (Task)invocation.ReturnValue;
        await task;
    }

    private async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation, OperateLogAttribute attribute)
    {
        TResult result;

        var methodInfo = invocation.Method ?? invocation.MethodInvocationTarget;
        var fullName = methodInfo.DeclaringType?.FullName ?? string.Empty;
        var startTime = DateTime.Now;
        var log = CreateOpsLog(fullName, methodInfo.Name, attribute.LogName, invocation.Arguments, userContext);

        try
        {
            invocation.Proceed();
            var task = (Task<TResult>)invocation.ReturnValue;
            result = await task;
            log.Succeed = true;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            log.ExecutionTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
            WriteOpsLog(log);
        }
        return result;
    }

    private static async Task<TResult> InternalInterceptAsynchronousWithOutOpsLog<TResult>(IInvocation invocation)
    {
        TResult result;
        invocation.Proceed();
        var task = (Task<TResult>)invocation.ReturnValue;
        result = await task;

        return result;
    }

    private static OperationLog CreateOpsLog(string className, string methodName, string logName, object[] arguments, UserContext userContext)
    {
        var message = string.Empty;
        if (arguments is not null)
        {
            message = JsonSerializer.Serialize(arguments, SystemTextJson.GetXWDefaultOptions());
            if (message.Length > 1000)
            {
                message = message.Substring(0, 1000);
            }
        }

        var log = new OperationLog
        {
            Id = IdGenerater.GetNextId(),
            ClassName = className,
            CreateTime = DateTime.Now,
            LogName = logName,
            LogType = "Operation Log",
            Message = message,
            Method = methodName,
            Succeed = false,
            UserId = userContext.Id,
            Name = userContext.Name,
            Account = userContext.Account,
            RemoteIpAddress = userContext.RemoteIpAddress
        };
        return log;
    }

    private void WriteOpsLog(OperationLog logInfo)
    {
        try
        {
            //var properties = _mqProducer.CreateBasicProperties();
            //// Enable message persistence
            //properties.Persistent = true;
            //_mqProducer.BasicPublish(MqExchanges.Logs, MqRoutingKeys.OpsLog, logInfo, properties);
            Channels.ChannelAccessor<OperationLog>.Instance.Writer.WriteAsync(logInfo).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WriteOpsLog error");
        }
    }

    /// <summary>获取方法上的 <see cref="OperateLogAttribute"/>。</summary>
    private static OperateLogAttribute? GetAttribute(IInvocation invocation)
    {
        var methodInfo = invocation.Method ?? invocation.MethodInvocationTarget;
        var attribute = methodInfo.GetCustomAttribute<OperateLogAttribute>();
        return attribute;
    }
}
