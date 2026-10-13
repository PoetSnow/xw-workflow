using Microsoft.Extensions.DependencyInjection;
using XW.Infra.Core.DependencyInjection;
using XW.Shared.Application.Contracts.Interfaces;
using XW.Shared.Application.Contracts.ResultModels;
using XW.Shared.Application.Mapper;
using System.Linq.Expressions;
using System.Net;

namespace XW.Shared.Application.Services;

/// <summary>
/// 应用服务基类：提供 Mapper、统一返回 <see cref="ServiceResult"/> / <see cref="ProblemDetails"/> 等辅助方法。
/// </summary>
public abstract class AbstractAppService : IAppService
{
    public static IObjectMapper Mapper
    {
        get => ServiceLocator.GetProvider().GetRequiredService<IObjectMapper>();
    }

    protected static ServiceResult ServiceResult() => new();

    protected static ServiceResult<TValue> ServiceResult<TValue>(TValue value)
        where TValue : notnull
    {
        return new ServiceResult<TValue>(value);
    }

    protected static ProblemDetails Problem(HttpStatusCode? statusCode = null, string? detail = null, string? title = null, string? instance = null, string? type = null) => new(statusCode, detail, title, instance, type);

    protected static Expression<Func<TEntity, object>>[] UpdatingProps<TEntity>(params Expression<Func<TEntity, object>>[] expressions) => expressions;
}
