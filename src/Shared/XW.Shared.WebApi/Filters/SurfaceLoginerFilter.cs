using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using XW.Shared.WebApi.Authentication.Bearer;

namespace XW.Shared.WebApi.Filters;

/// <summary>Portal JWT 与 Console JWT 互访对方前缀时拒绝。四诊仪入口不走本过滤器。</summary>
public sealed class SurfaceLoginerFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var expected = context.Controller switch
        {
            PortalApiController => BearerDefaults.Customer,
            ConsoleApiController => BearerDefaults.Manager,
            _ => null
        };
        if (expected is null)
        {
            await next();
            return;
        }

        var actual = user.FindFirst(BearerDefaults.LoginerType)?.Value;
        if (string.IsNullOrEmpty(actual) || string.Equals(actual, expected, StringComparison.Ordinal))
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = "身份不匹配",
            Detail = "当前令牌不能访问该入口",
            Status = StatusCodes.Status403Forbidden,
            Instance = context.HttpContext.Request.Path
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
