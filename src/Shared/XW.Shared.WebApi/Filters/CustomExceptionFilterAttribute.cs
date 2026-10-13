using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XW.Infra.Core.Exceptions;
using XW.Shared;

namespace Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// Exception interceptor for exceptions with StatusCode >= 500
/// </summary>
public sealed class CustomExceptionFilterAttribute(ILogger<CustomExceptionFilterAttribute> logger, IWebHostEnvironment env) : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var status = 500;
        var exception = context.Exception;
        var requestId = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        var eventId = new EventId(exception.HResult, requestId);
        var userContext = context.HttpContext.RequestServices.GetService<UserContext>();
        // var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        //string className = descriptor.ControllerName;
        //string method = descriptor.ActionName;
        var hostAndPort = context.HttpContext.Request.Host.HasValue ? context.HttpContext.Request.Host.Value : string.Empty;
        var requestUrl = string.Concat(hostAndPort, context.HttpContext.Request.Path);
        var type = string.Concat("https://httpstatuses.com/", status);

        string title;
        string detial;
        if (exception is IXWException adncException)
        {
            title = "Business exception";
            status = adncException.Status;
            detial = exception.Message;
        }
        else
        {
            title = env.IsDevelopment() ? exception.Message : "System exception";
            detial = env.IsDevelopment() ? exception.GetDetail() : $"System exception, please contact the administrator ({eventId})";
            logger.LogError(eventId, exception, "{userId}:{requestUrl}", userContext?.Id, requestUrl);
        }
        var problemDetails = new ProblemDetails { Title = title, Detail = detial, Type = type, Status = status };

        context.Result = new ObjectResult(problemDetails) { StatusCode = status };
        context.ExceptionHandled = true;
    }

    public override Task OnExceptionAsync(ExceptionContext context)
    {
        OnException(context);
        return Task.CompletedTask;
    }
}
