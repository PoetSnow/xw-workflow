using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Knowledge;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Services;

/// <summary>
/// PostgreSQL 审计存储。与知识库共用 <see cref="KnowledgePgDataSource"/> 连接串。
/// 未配置连接串时由 DI 回退到 <see cref="InMemoryAgentAuditStore"/>。
/// </summary>
public sealed class PgAgentAuditStore(
    KnowledgePgDataSource dataSource,
    IOptionsMonitor<AgentOptions> options,
    ILogger<PgAgentAuditStore> logger) : IAgentAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public async Task AppendAsync(AgentAuditRecord record, CancellationToken ct = default)
    {
        RequireConnectionString();

        const string sql = """
            INSERT INTO agent_audit_record (
                trace_id, session_id, user_id, tenant_id, intent,
                user_message, assistant_reply, tool_calls,
                permission_denied, approval_required, created_at)
            VALUES (
                @traceId, @sessionId, @userId, @tenantId, @intent,
                @userMessage, @assistantReply, @toolCalls::jsonb,
                @permissionDenied, @approvalRequired, @createdAt)
            """;

        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("traceId", record.TraceId);
        cmd.Parameters.AddWithValue("sessionId", record.SessionId);
        cmd.Parameters.AddWithValue("userId", (object?)record.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tenantId", (object?)record.TenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("intent", (object?)record.Intent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("userMessage", record.UserMessage);
        cmd.Parameters.AddWithValue("assistantReply", record.AssistantReply);
        cmd.Parameters.AddWithValue("toolCalls", JsonSerializer.Serialize(record.ToolCalls, JsonOptions));
        cmd.Parameters.AddWithValue("permissionDenied", record.PermissionDenied);
        cmd.Parameters.AddWithValue("approvalRequired", record.ApprovalRequired);
        cmd.Parameters.AddWithValue("createdAt", record.At.ToUniversalTime());

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogError(
                ex,
                "缺少表 agent_audit_record。请执行 docs/sql/xw_agent.postgresql.sql");
            throw new InvalidOperationException(
                "缺少表 agent_audit_record。请执行 docs/sql/xw_agent.postgresql.sql 后重试。", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentAuditRecord>> ListBySessionAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        RequireConnectionString();

        const string sql = """
            SELECT trace_id, session_id, user_id, tenant_id, intent,
                   user_message, assistant_reply, tool_calls,
                   permission_denied, approval_required, created_at
            FROM agent_audit_record
            WHERE session_id = @sessionId
            ORDER BY created_at ASC, id ASC
            """;

        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("sessionId", sessionId);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<AgentAuditRecord>();
            while (await reader.ReadAsync(ct))
            {
                var toolJson = reader.IsDBNull(7) ? "[]" : reader.GetString(7);
                var tools = JsonSerializer.Deserialize<List<ToolExecutionResult>>(toolJson, JsonOptions)
                            ?? [];

                list.Add(new AgentAuditRecord
                {
                    TraceId = reader.GetString(0),
                    SessionId = reader.GetString(1),
                    UserId = reader.IsDBNull(2) ? null : reader.GetString(2),
                    TenantId = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Intent = reader.IsDBNull(4) ? null : reader.GetString(4),
                    UserMessage = reader.GetString(5),
                    AssistantReply = reader.GetString(6),
                    ToolCalls = tools,
                    PermissionDenied = reader.GetBoolean(8),
                    ApprovalRequired = reader.GetBoolean(9),
                    At = reader.GetFieldValue<DateTimeOffset>(10)
                });
            }

            return list;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogError(
                ex,
                "缺少表 agent_audit_record。请执行 docs/sql/xw_agent.postgresql.sql");
            throw new InvalidOperationException(
                "缺少表 agent_audit_record。请执行 docs/sql/xw_agent.postgresql.sql 后重试。", ex);
        }
    }

    private void RequireConnectionString()
    {
        if (string.IsNullOrWhiteSpace(options.CurrentValue.Knowledge.ConnectionString))
        {
            throw new InvalidOperationException(
                "未配置 Agent:Knowledge:ConnectionString，无法使用 PostgreSQL 审计存储。");
        }
    }
}
