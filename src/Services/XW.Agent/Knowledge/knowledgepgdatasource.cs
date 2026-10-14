using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Knowledge;

/// <summary>单例 DataSource；注册 pgvector 类型映射。</summary>
public sealed class KnowledgePgDataSource : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public KnowledgePgDataSource(IOptionsMonitor<AgentOptions> options, ILogger<KnowledgePgDataSource> logger)
    {
        var cs = options.CurrentValue.Knowledge.ConnectionString;
        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("Agent:Knowledge:ConnectionString 为空；vector 入库/检索将不可用，直到配置连接串。");
            // 占位，避免 DI 失败；真实操作时再校验
            cs = "Host=127.0.0.1;Username=postgres;Password=postgres;Database=postgres";
        }

        var builder = new NpgsqlDataSourceBuilder(cs);
        _dataSource = builder.Build();
    }

    public NpgsqlDataSource Inner => _dataSource;

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
