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
/// PostgreSQL 会话存储。与审计、知识库共用 <see cref="KnowledgePgDataSource"/>。
/// 待办和幂等键在 slots jsonb 里，不另建任务表。未配置连接串时 DI 回退内存存储。
/// </summary>
public sealed class PgAgentSessionStore(
    KnowledgePgDataSource dataSource,
    IOptionsMonitor<AgentOptions> options,
    ILogger<PgAgentSessionStore> logger) : IAgentSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <inheritdoc />
    public async Task<AgentSession> GetOrCreateAsync(string? sessionId, string? userId, CancellationToken ct = default)
    {
        RequireConnectionString();
        var id = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId.Trim();

        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        var existing = await LoadAsync(conn, id, ct);
        if (existing is not null)
        {
            existing.UserId ??= userId;
            existing.CaptureBaseline();
            return existing;
        }

        const string insert = """
            INSERT INTO agent_session (id, user_id, slots, version, created_at, updated_at)
            VALUES (@id, @userId, '{}'::jsonb, 1, now(), now())
            ON CONFLICT (id) DO NOTHING
            """;
        await using (var cmd = new NpgsqlCommand(insert, conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("userId", (object?)userId ?? DBNull.Value);
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                throw MissingTable(ex);
            }
        }

        var loaded = await LoadAsync(conn, id, ct)
                     ?? throw new InvalidOperationException($"会话 {id} 写入后未能读回。");
        loaded.CaptureBaseline();
        return loaded;
    }

    /// <inheritdoc />
    public async Task SaveAsync(AgentSession session, CancellationToken ct = default)
    {
        RequireConnectionString();
        if (await TryUpdateAsync(session, ct))
        {
            return;
        }

        // 版本对不上：读出对方结果，只套用本轮改过的键。同一键分歧则拒绝覆盖。
        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        var stored = await LoadAsync(conn, session.Id, ct)
                     ?? throw new SessionConcurrencyException(null);
        var merged = SessionWriteMerger.Merge(
            session.SlotBaseline,
            session.Slots,
            stored.Slots,
            session.Messages,
            session.MessageBaseline,
            stored.Messages);
        if (!merged.Accepted)
        {
            throw new SessionConcurrencyException(merged.ConflictKey);
        }

        session.Slots.Clear();
        foreach (var (key, value) in merged.Slots)
        {
            session.Slots[key] = value;
        }

        session.Messages.Clear();
        session.Messages.AddRange(merged.Messages);
        session.Version = stored.Version;
        if (!await TryUpdateAsync(session, ct))
        {
            throw new SessionConcurrencyException(null);
        }
    }

    private async Task<bool> TryUpdateAsync(AgentSession session, CancellationToken ct)
    {
        const string update = """
            UPDATE agent_session
            SET user_id = @userId,
                tenant_id = @tenantId,
                slots = @slots::jsonb,
                version = version + 1,
                updated_at = @updatedAt
            WHERE id = @id AND version = @version
            """;

        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand(update, conn, tx);
        cmd.Parameters.AddWithValue("id", session.Id);
        cmd.Parameters.AddWithValue("userId", (object?)session.UserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tenantId", (object?)session.TenantId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("slots", JsonSerializer.Serialize(session.Slots, JsonOptions));
        cmd.Parameters.AddWithValue("updatedAt", DateTimeOffset.UtcNow);
        cmd.Parameters.AddWithValue("version", session.Version);

        int updated;
        try
        {
            updated = await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw MissingTable(ex);
        }

        if (updated != 1)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        await ReplaceMessagesAsync(conn, tx, session, ct);
        await tx.CommitAsync(ct);
        session.Version++;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        session.CaptureBaseline();
        logger.LogDebug("Session {SessionId} saved at version {Version}", session.Id, session.Version);
        return true;
    }

    private static async Task ReplaceMessagesAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        AgentSession session,
        CancellationToken ct)
    {
        await using (var delete = new NpgsqlCommand(
            "DELETE FROM agent_session_message WHERE session_id = @id", conn, tx))
        {
            delete.Parameters.AddWithValue("id", session.Id);
            await delete.ExecuteNonQueryAsync(ct);
        }

        const string insert = """
            INSERT INTO agent_session_message (session_id, role, content, intent, created_at)
            VALUES (@sessionId, @role, @content, @intent, @createdAt)
            """;
        foreach (var message in session.Messages)
        {
            await using var cmd = new NpgsqlCommand(insert, conn, tx);
            cmd.Parameters.AddWithValue("sessionId", session.Id);
            cmd.Parameters.AddWithValue("role", message.Role);
            cmd.Parameters.AddWithValue("content", message.Content);
            cmd.Parameters.AddWithValue("intent", (object?)message.Intent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("createdAt", message.At.ToUniversalTime());
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<AgentSession?> LoadAsync(NpgsqlConnection conn, string id, CancellationToken ct)
    {
        const string sql = """
            SELECT user_id, tenant_id, slots::text, version, created_at, updated_at
            FROM agent_session
            WHERE id = @id
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            var slotsJson = reader.IsDBNull(2) ? "{}" : reader.GetString(2);
            var slots = JsonSerializer.Deserialize<Dictionary<string, string>>(slotsJson)
                        ?? new Dictionary<string, string>();
            var session = new AgentSession
            {
                Id = id,
                UserId = reader.IsDBNull(0) ? null : reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? null : reader.GetString(1),
                Version = reader.GetInt32(3),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(4),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(5),
                Slots = new Dictionary<string, string>(slots, StringComparer.OrdinalIgnoreCase)
            };
            await reader.CloseAsync();
            session.Messages.AddRange(await LoadMessagesAsync(conn, id, ct));
            return session;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw MissingTable(ex);
        }
    }

    private static async Task<List<AgentMessage>> LoadMessagesAsync(
        NpgsqlConnection conn,
        string id,
        CancellationToken ct)
    {
        const string sql = """
            SELECT role, content, intent, created_at
            FROM agent_session_message
            WHERE session_id = @id
            ORDER BY id ASC
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AgentMessage>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AgentMessage
            {
                Role = reader.GetString(0),
                Content = reader.GetString(1),
                Intent = reader.IsDBNull(2) ? null : reader.GetString(2),
                At = reader.GetFieldValue<DateTimeOffset>(3)
            });
        }

        return list;
    }

    private void RequireConnectionString()
    {
        if (string.IsNullOrWhiteSpace(options.CurrentValue.Knowledge.ConnectionString))
        {
            throw new InvalidOperationException(
                "未配置 Agent:Knowledge:ConnectionString，无法使用 PostgreSQL 会话存储。");
        }
    }

    private static InvalidOperationException MissingTable(PostgresException ex)
    {
        return new InvalidOperationException(
            "缺少表 agent_session / agent_session_message。请执行 docs/sql/xw_agent.postgresql.sql 后重试。",
            ex);
    }
}
