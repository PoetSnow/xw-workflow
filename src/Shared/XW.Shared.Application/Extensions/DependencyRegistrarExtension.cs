using XW.Shared.Application.Registrar;
using Polly;
using Polly.Timeout;

namespace XW.Shared.Application.Extensions;

/// <summary>
/// <see cref="AbstractApplicationDependencyRegistrar"/> 扩展：环境名、默认 Polly 策略等。
/// </summary>
public static class DependencyRegistrarExtension
{
    public static string ASPNETCORE_ENVIRONMENT => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? throw new ArgumentNullException("ASPNETCORE_ENVIRONMENT is null");

    /// <summary>
    /// 生成 Refit REST 客户端默认 Polly 策略（重试、超时、熔断）。
    /// </summary>
    public static List<IAsyncPolicy<HttpResponseMessage>> GenerateDefaultRefitPolicies(this AbstractApplicationDependencyRegistrar _)
    {
        // 舱壁隔离策略
        //var bulkheadPolicy = Policy.BulkheadAsync<HttpResponseMessage>(10, 100);

        // 降级策略：发生故障时使用备用数据，当前未启用
        //var fallbackPolicy = Policy<string>.Handle<HttpRequestException>().FallbackAsync("substitute data");

        // 缓存策略对 HttpClientFactory 场景效果有限，见 Polly 文档
        //https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory

        // 重试：超时或 5xx 时重试 3 次
        var retryPolicy = Policy.Handle<TimeoutRejectedException>()
                                .OrResult<HttpResponseMessage>(response => (int)response.StatusCode >= 500)
                                .WaitAndRetryAsync(
                                [
                                TimeSpan.FromSeconds(3),
                                TimeSpan.FromSeconds(5),
                                ]);
        // 超时策略
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(_.IsDevelopment() ? 10 : 9);

        // 熔断：连续失败达到阈值后打开熔断，冷却期后半开探测
        var circuitBreakerPolicy = Policy.Handle<Exception>()
                                         .CircuitBreakerAsync
                                         (
                                             exceptionsAllowedBeforeBreaking: 10,
                                             durationOfBreak: TimeSpan.FromMinutes(3),
                                             onBreak: (ex, breakDelay) =>
                                             {
                                                 var e = ex;
                                                 var delay = breakDelay;
                                             },
                                             onReset: () =>
                                             {
                                             },
                                             onHalfOpen: () =>
                                             {
                                             }
                                         );

        return
                    [
                        retryPolicy
                       ,timeoutPolicy
                       ,circuitBreakerPolicy.AsAsyncPolicy<HttpResponseMessage>()
                    ];
    }

    /// <summary>
    /// 生成 gRPC 客户端默认 Polly 策略（与 REST 相同）。
    /// </summary>
    public static List<IAsyncPolicy<HttpResponseMessage>> GenerateDefaultGrpcPolicies(this AbstractApplicationDependencyRegistrar registrar) =>
        registrar.GenerateDefaultRefitPolicies();

    public static bool IsDevelopment(this AbstractApplicationDependencyRegistrar _) => ASPNETCORE_ENVIRONMENT.EqualsIgnoreCase("Development");

    /// <summary>
    /// 将 ASPNETCORE_ENVIRONMENT 转为 CAP 分组等使用的短环境名。
    /// </summary>
    public static string GetEnvShortName(this AbstractApplicationDependencyRegistrar _)
    {
        return ASPNETCORE_ENVIRONMENT.ToLower() switch
        {
            "development" => "dev",
            "test" => "test",
            "staging" => $"stag",
            "production" => $"prod",
            _ => throw new InvalidDataException(nameof(ASPNETCORE_ENVIRONMENT))
        };
    }
}
