using System.Text.Json;

namespace XW.Agent.Application.Models;

/// <summary>调用方写入会话的资料集编号。模型参数不能改这把钥匙。</summary>
public static class SessionCorpus
{
    /// <summary>槽位键。只由调用方请求写入。</summary>
    public const string SlotKey = "_corpus_ids";

    /// <summary>读出资料集编号。没有则为空列表。</summary>
    public static IReadOnlyList<string> Read(AgentSession session)
    {
        if (!session.Slots.TryGetValue(SlotKey, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw)?
                       .Select(id => id?.Trim() ?? string.Empty)
                       .Where(id => id.Length > 0)
                       .Distinct(StringComparer.Ordinal)
                       .ToArray()
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>用调用方这次传来的编号覆盖会话里的资料集。空列表表示清空。</summary>
    public static void Write(AgentSession session, IReadOnlyList<string>? corpusIds)
    {
        var ids = (corpusIds ?? [])
            .Select(id => id?.Trim() ?? string.Empty)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            session.Slots.Remove(SlotKey);
            return;
        }

        session.Slots[SlotKey] = JsonSerializer.Serialize(ids);
    }
}
