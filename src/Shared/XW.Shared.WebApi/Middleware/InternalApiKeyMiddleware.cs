using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace XW.Shared.WebApi.Middleware;

/// <summary>
/// 可选 Internal API Key 校验（配置 InternalApi:ApiKey 后生效）。
/// </summary>
public sealed class InternalApiKeyMiddleware(RequestDelegate next, IOptions<InternalApiOptions> options)
{
    public const string HeaderName = "X-Internal-Api-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        var configuredKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            await next(context);
            return;
        }

        if (!context.Request.Path.StartsWithSegments("/api/internal", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !string.Equals(provided.ToString(), configuredKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { title = "Unauthorized", detail = "Invalid or missing Internal API Key" });
            return;
        }

        await next(context);
    }
}

public sealed class InternalApiOptions
{
    public const string SectionName = "InternalApi";

    public string ApiKey { get; set; } = string.Empty;
}

public static class InternalApiMiddlewareExtensions
{
    public static IServiceCollection AddInternalApiKeyAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InternalApiOptions>(configuration.GetSection(InternalApiOptions.SectionName));
        return services;
    }

    public static IApplicationBuilder UseInternalApiKeyAuth(this IApplicationBuilder app) =>
        app.UseMiddleware<InternalApiKeyMiddleware>();
}
