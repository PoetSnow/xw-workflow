using System.Collections.Concurrent;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Services;

/// <summary>
/// 无 PostgreSQL 连接串时的会话存储。进程内有效，重启丢失。
/// 读、写都复制一份，避免调用方继续改槽位时把已保存的幂等键带脏。
/// </summary>
public sealed class InMemoryAgentSessionStore : IAgentSessionStore
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <inheritdoc />
    public Task<AgentSession> GetOrCreateAsync(string? sessionId, string? userId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return GetOrCreateCore(sessionId, userId);
        }
    }

    private Task<AgentSession> GetOrCreateCore(string? sessionId, string? userId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId) && _sessions.TryGetValue(sessionId, out var existing))
        {
            var loaded = Clone(existing);
            loaded.UserId ??= userId;
            loaded.CaptureBaseline();
            return Task.FromResult(loaded);
        }

        var session = new AgentSession
        {
            Id = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId.Trim(),
            UserId = userId,
            Version = 1
        };
        session.CaptureBaseline();
        _sessions[session.Id] = Clone(session);
        return Task.FromResult(session);
    }

    /// <inheritdoc />
    public Task SaveAsync(AgentSession session, CancellationToken ct = default)
    {
        lock (_gate)
        {
            SaveCore(session);
        }

        return Task.CompletedTask;
    }

    private void SaveCore(AgentSession session)
    {
        if (_sessions.TryGetValue(session.Id, out var stored) && stored.Version != session.Version)
        {
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
        }

        session.UpdatedAt = DateTimeOffset.UtcNow;
        session.Version++;
        session.CaptureBaseline();
        _sessions[session.Id] = Clone(session);
    }

    private static AgentSession Clone(AgentSession source)
    {
        var copy = new AgentSession
        {
            Id = source.Id,
            UserId = source.UserId,
            TenantId = source.TenantId,
            Version = source.Version,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
        foreach (var (key, value) in source.Slots)
        {
            copy.Slots[key] = value;
        }

        copy.Messages.AddRange(source.Messages.Select(m => new AgentMessage
        {
            Role = m.Role,
            Content = m.Content,
            Intent = m.Intent,
            At = m.At
        }));
        return copy;
    }
}

/// <summary>联调用内存审计；生产由 <see cref="PgAgentAuditStore"/> 落库。</summary>
public sealed class InMemoryAgentAuditStore : IAgentAuditStore
{
    private readonly ConcurrentDictionary<string, List<AgentAuditRecord>> _bySession = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task AppendAsync(AgentAuditRecord record, CancellationToken ct = default)
    {
        var list = _bySession.GetOrAdd(record.SessionId, _ => []);
        lock (list)
        {
            list.Add(record);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentAuditRecord>> ListBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_bySession.TryGetValue(sessionId, out var list))
        {
            return Task.FromResult<IReadOnlyList<AgentAuditRecord>>([]);
        }

        lock (list)
        {
            return Task.FromResult<IReadOnlyList<AgentAuditRecord>>(list.ToList());
        }
    }
}
