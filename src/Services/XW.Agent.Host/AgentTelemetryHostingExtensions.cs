using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XW.Agent.Application.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace XW.Agent.Host;

/// <summary>Sprint 1：OpenTelemetry + Agent Trace 接入。</summary>
public static class AgentTelemetryHostingExtensions
{
    public static IServiceCollection AddXwAgentTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection("Telemetry");
        var enabled = section.GetValue("Enabled", true);
        if (!enabled)
        {
            return services;
        }

        var console = section.GetValue("ConsoleExporter", environment.IsDevelopment());
        var otlp = section["OtlpEndpoint"];
        // Aspire Dashboard / 运维注入的标准环境变量
        var otlpFromEnv = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(
                    serviceName: AgentTelemetry.ServiceName,
                    serviceVersion: typeof(AgentTelemetryHostingExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment.EnvironmentName
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(AgentTelemetry.ActivitySourceName)
                    .AddSource("XW.Agent.Llm")
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments("/health")
                            && !ctx.Request.Path.StartsWithSegments("/swagger");
                    })
                    .AddHttpClientInstrumentation();

                if (console)
                {
                    tracing.AddConsoleExporter();
                }

                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
                }
                else if (!string.IsNullOrWhiteSpace(otlpFromEnv))
                {
                    // Aspire：不写死 endpoint，走 OTEL_EXPORTER_OTLP_* 环境变量
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                // 与 Tracing 共用 Console / OTLP，不另开导出通道。
                metrics.AddMeter(AgentTelemetry.ServiceName);
                if (console)
                {
                    metrics.AddConsoleExporter();
                }

                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
                }
                else if (!string.IsNullOrWhiteSpace(otlpFromEnv))
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }
}
