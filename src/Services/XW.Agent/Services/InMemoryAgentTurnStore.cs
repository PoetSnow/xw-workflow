using System.Collections.Concurrent;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Services;

/// <summary>无连接串时的轮次存储。进程内有效，重启丢失。</summary>
public sealed class InMemoryAgentTurnStore : IAgentTurnStore
{
    private readonly ConcurrentDictionary<string, List<AgentTurnRecord>> _bySession = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task AppendAsync(AgentTurnRecord record, CancellationToken ct = default)
    {
        var list = _bySession.GetOrAdd(record.SessionId, _ => []);
        lock (list)
        {
            list.Add(record);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentTurnRecord>> ListBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_bySession.TryGetValue(sessionId, out var list))
        {
            return Task.FromResult<IReadOnlyList<AgentTurnRecord>>([]);
        }

        lock (list)
        {
            return Task.FromResult<IReadOnlyList<AgentTurnRecord>>(list.ToList());
        }
    }
}
