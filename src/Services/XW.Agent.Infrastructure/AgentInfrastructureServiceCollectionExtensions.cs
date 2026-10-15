using DotNetCore.CAP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XW.Agent.Contracts.Jobs;
using XW.Agent.Contracts.Knowledge;
using XW.Agent.Infrastructure.Jobs;
using XW.Agent.Infrastructure.Knowledge;

namespace XW.Agent.Infrastructure;

/// <summary>注册检索、入库、作业分发。CAP 仅在配置开启时注册。</summary>
public static class AgentInfrastructureServiceCollectionExtensions
{
    /// <summary>注册底座基础设施。</summary>
    public static IServiceCollection AddXwAgentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KnowledgeConnectionOptions>(configuration.GetSection(KnowledgeConnectionOptions.SectionName));
        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));
        services.Configure<JobTransportOptions>(configuration.GetSection(JobTransportOptions.SectionName));

        services.AddSingleton<KnowledgeVectorDataSource>();
        services.AddHttpClient(OpenAiCompatibleEmbeddingClient.HttpClientName, (sp, client) =>
        {
            var seconds = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<EmbeddingOptions>>()
                .CurrentValue.TimeoutSeconds;
            client.Timeout = TimeSpan.FromSeconds(seconds <= 0 ? 60 : seconds);
        });
        services.AddSingleton<IEmbeddingClient, OpenAiCompatibleEmbeddingClient>();
        services.AddSingleton<PlainTextDocumentExtractor>();
        services.AddSingleton<PdfDocumentExtractor>();
        services.AddSingleton<UnsupportedBinaryDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor>(sp => new CompositeDocumentTextExtractor(
        [
            sp.GetRequiredService<PdfDocumentExtractor>(),
            sp.GetRequiredService<PlainTextDocumentExtractor>(),
            sp.GetRequiredService<UnsupportedBinaryDocumentExtractor>()
        ]));
        services.AddSingleton<IKnowledgeVectorStore, PgVectorKnowledgeStore>();
        services.AddSingleton<IKnowledgeIngest, KnowledgeIngestService>();
        services.AddSingleton<IKnowledgeRetriever, VectorKnowledgeRetriever>();

        services.AddSingleton<IJobDispatcher>(sp => new JobDispatcher(sp.GetServices<IJobHandler>()));

        var transport = configuration.GetSection(JobTransportOptions.SectionName).Get<JobTransportOptions>()
                        ?? new JobTransportOptions();
        if (transport.CapEnabled)
        {
            var storage = string.IsNullOrWhiteSpace(transport.StorageConnectionString)
                ? configuration.GetSection(KnowledgeConnectionOptions.SectionName)["ConnectionString"]
                : transport.StorageConnectionString;
            if (string.IsNullOrWhiteSpace(storage))
            {
                throw new InvalidOperationException(
                    "Agent:Jobs:CapEnabled=true 时需要 StorageConnectionString 或 Agent:Knowledge:ConnectionString。");
            }

            services.AddCap(cap =>
            {
                cap.UsePostgreSql(storage);
                cap.UseRabbitMQ(rabbit =>
                {
                    rabbit.HostName = transport.RabbitHost;
                    rabbit.Port = transport.RabbitPort;
                    rabbit.UserName = transport.RabbitUserName;
                    rabbit.Password = transport.RabbitPassword;
                });
            });
            services.AddTransient<ICapSubscribe, JobCapSubscriber>();
        }

        return services;
    }
}
