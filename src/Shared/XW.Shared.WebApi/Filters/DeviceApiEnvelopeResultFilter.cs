using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using XW.Shared.WebApi.Models;
using XW.Shared.WebApi.Routing;

namespace XW.Shared.WebApi.Filters;

/// <summary>
/// 将设备域 API 响应统一包装为 { code, result, message, success }。
/// </summary>
public sealed class DeviceApiEnvelopeResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!IsDeviceApiRequest(context.HttpContext.Request.Path))
        {
            return;
        }

        if (context.Result is DeviceApiEnvelopeObjectResult)
        {
            return;
        }

        switch (context.Result)
        {
            case ObjectResult { Value: DeviceApiEnvelope }:
                return;
            case ObjectResult objectResult:
                context.Result = WrapObjectResult(objectResult);
                break;
            case EmptyResult or NoContentResult:
                context.Result = DeviceApiEnvelopeObjectResult.Ok(null);
                break;
            case NotFoundResult:
                context.Result = DeviceApiEnvelopeObjectResult.Fail(404, "资源不存在");
                break;
            case UnauthorizedResult:
                context.Result = DeviceApiEnvelopeObjectResult.Fail(401, "未授权");
                break;
            case ForbidResult:
                context.Result = DeviceApiEnvelopeObjectResult.Fail(403, "禁止访问");
                break;
            case StatusCodeResult statusCodeResult:
                context.Result = DeviceApiEnvelopeObjectResult.Fail(
                    statusCodeResult.StatusCode,
                    DefaultMessage(statusCodeResult.StatusCode));
                break;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static bool IsDeviceApiRequest(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith($"/{ApiSurfaces.DeviceRoutePrefix.TrimStart('/')}", StringComparison.OrdinalIgnoreCase);
    }

    private static IActionResult WrapObjectResult(ObjectResult objectResult)
    {
        if (objectResult.Value is DeviceApiEnvelope envelope)
        {
            return DeviceApiEnvelopeObjectResult.FromEnvelope(envelope, objectResult.StatusCode);
        }

        if (objectResult.Value is ProblemDetails problem)
        {
            var code = problem.Status ?? objectResult.StatusCode ?? 400;
            return DeviceApiEnvelopeObjectResult.Fail(code, problem.Detail ?? problem.Title ?? "请求失败");
        }

        if (objectResult.StatusCode is >= 400)
        {
            var code = objectResult.StatusCode ?? 400;
            var message = objectResult.Value?.ToString() ?? DefaultMessage(code);
            return DeviceApiEnvelopeObjectResult.Fail(code, message);
        }

        return DeviceApiEnvelopeObjectResult.Ok(objectResult.Value);
    }

    private static string DefaultMessage(int statusCode) => statusCode switch
    {
        401 => "未授权",
        403 => "禁止访问",
        404 => "资源不存在",
        _ => "请求失败"
    };
}

internal sealed class DeviceApiEnvelopeObjectResult : ObjectResult
{
    private DeviceApiEnvelopeObjectResult(object? value, int statusCode) : base(value)
    {
        StatusCode = statusCode;
    }

    public static DeviceApiEnvelopeObjectResult Ok(object? result, string message = "success") =>
        new(DeviceApiEnvelope.Ok(result, message), StatusCodes.Status200OK);

    public static DeviceApiEnvelopeObjectResult Fail(int code, string message) =>
        new(DeviceApiEnvelope.Fail(code, message), StatusCodes.Status200OK);

    public static DeviceApiEnvelopeObjectResult FromEnvelope(DeviceApiEnvelope envelope, int? statusCode) =>
        new(envelope, statusCode is >= 400 ? StatusCodes.Status200OK : StatusCodes.Status200OK);
}
