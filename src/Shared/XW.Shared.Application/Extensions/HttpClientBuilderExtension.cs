using Polly;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 为 <see cref="IHttpClientBuilder"/> 批量添加 Polly 策略。
/// </summary>
public static class HttpClientBuilderExtension
{
    public static IHttpClientBuilder AddPolicyHandlers(this IHttpClientBuilder builder, List<IAsyncPolicy<HttpResponseMessage>> policies)
    {
        policies?.ForEach(policy => builder.AddPolicyHandler(policy));
        return builder;
    }
}
