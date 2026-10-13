using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Knowledge;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Services;

/// <summary>
/// PostgreSQL 轮次存储，与审计、会话共用 <see cref="KnowledgePgDataSource"/>。
/// 未配置连接串时 DI 回退到 <see cref="InMemoryAgentTurnStore"/>。
/// </summary>
public sealed class PgAgentTurnStore(
    KnowledgePgDataSource dataSource,
    IOptionsMonitor<AgentOptions> options,
    ILogger<PgAgentTurnStore> logger) : IAgentTurnStore
{
    /// <inheritdoc />
    public async Task AppendAsync(AgentTurnRecord record, CancellationToken ct = default)
    {
        RequireConnectionString();

        const string sql = """
            INSERT INTO agent_turn (
                trace_id, session_id, lane, lane_reason, phase, analysis, created_at)
            VALUES (
                @traceId, @sessionId, @lane, @laneReason, @phase, @analysis::jsonb, @createdAt)
            """;

        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("traceId", record.TraceId);
        cmd.Parameters.AddWithValue("sessionId", record.SessionId);
        cmd.Parameters.AddWithValue("lane", (object?)record.Lane ?? DBNull.Value);
        cmd.Parameters.AddWithValue("laneReason", (object?)record.LaneReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phase", (object?)record.Phase ?? DBNull.Value);
        cmd.Parameters.AddWithValue("analysis", string.IsNullOrWhiteSpace(record.AnalysisJson) ? "{}" : record.AnalysisJson);
        cmd.Parameters.AddWithValue("createdAt", record.CreatedAt.ToUniversalTime());

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogError(ex, "缺少表 agent_turn。请执行 docs/sql/xw_agent.postgresql.sql");
            throw new InvalidOperationException(
                "缺少表 agent_turn。请执行 docs/sql/xw_agent.postgresql.sql 后重试。", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentTurnRecord>> ListBySessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        RequireConnectionString();

        const string sql = """
            SELECT trace_id, session_id, lane, lane_reason, phase, analysis::text, created_at
            FROM agent_turn
            WHERE session_id = @sessionId
            ORDER BY created_at ASC, id ASC
            """;

        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("sessionId", sessionId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<AgentTurnRecord>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new AgentTurnRecord
                {
                    TraceId = reader.GetString(0),
                    SessionId = reader.GetString(1),
                    Lane = reader.IsDBNull(2) ? null : reader.GetString(2),
                    LaneReason = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Phase = reader.IsDBNull(4) ? null : reader.GetString(4),
                    AnalysisJson = reader.IsDBNull(5) ? "{}" : reader.GetString(5),
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(6)
                });
            }

            return list;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogError(ex, "缺少表 agent_turn。请执行 docs/sql/xw_agent.postgresql.sql");
            throw new InvalidOperationException(
                "缺少表 agent_turn。请执行 docs/sql/xw_agent.postgresql.sql 后重试。", ex);
        }
    }

    private void RequireConnectionString()
    {
        if (string.IsNullOrWhiteSpace(options.CurrentValue.Knowledge.ConnectionString))
        {
            throw new InvalidOperationException(
                "未配置 Agent:Knowledge:ConnectionString，无法使用 PostgreSQL 轮次存储。");
        }
    }
}
