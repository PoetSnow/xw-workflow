using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector.Npgsql;
using XW.Agent.Contracts.Knowledge;

namespace XW.Agent.Infrastructure.Knowledge;

/// <summary>向量库连接。注册 pgvector 类型映射。</summary>
public sealed class KnowledgeVectorDataSource : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    /// <summary>按知识库连接串建数据源。连接串为空时用占位，真实操作再拒绝。</summary>
    public KnowledgeVectorDataSource(
        IOptionsMonitor<KnowledgeConnectionOptions> options,
        ILogger<KnowledgeVectorDataSource> logger)
    {
        var cs = options.CurrentValue.ConnectionString;
        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("Agent:Knowledge:ConnectionString 为空；向量入库和检索不可用。");
            cs = "Host=127.0.0.1;Username=postgres;Password=postgres;Database=postgres";
        }

        var builder = new NpgsqlDataSourceBuilder(cs);
        builder.UseVector();
        _dataSource = builder.Build();
    }

    /// <summary>底层数据源。</summary>
    public NpgsqlDataSource Inner => _dataSource;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
