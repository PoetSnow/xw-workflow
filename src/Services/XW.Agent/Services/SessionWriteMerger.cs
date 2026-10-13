using XW.Agent.Application.Models;

namespace XW.Agent.Application.Services;

/// <summary>乐观锁冲突后的合并结果。</summary>
public sealed class SessionMergeResult
{
    /// <summary>能否在不覆盖分歧槽位的前提下写回。</summary>
    public bool Accepted { get; init; }

    /// <summary>拒绝时的槽位名。</summary>
    public string? ConflictKey { get; init; }

    /// <summary>合并后的槽位。拒绝时为空字典。</summary>
    public Dictionary<string, string> Slots { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>合并后的消息：对方已有的消息，再接上本轮新增。</summary>
    public List<AgentMessage> Messages { get; init; } = [];
}

/// <summary>
/// 会话写冲突合并。从对方当前槽位出发，只套用本轮相对基线的改动。
/// 同一键两边都改成了不同的值时拒绝，避免幂等键或待办步骤被无声换掉。
/// </summary>
public static class SessionWriteMerger
{
    public static SessionMergeResult Merge(
        IReadOnlyDictionary<string, string> baseline,
        IReadOnlyDictionary<string, string> ours,
        IReadOnlyDictionary<string, string> theirs,
        IReadOnlyList<AgentMessage> ourMessages,
        int messageBaseline,
        IReadOnlyList<AgentMessage> theirMessages)
    {
        var slots = new Dictionary<string, string>(theirs, StringComparer.OrdinalIgnoreCase);

        foreach (var key in baseline.Keys)
        {
            if (ours.ContainsKey(key))
            {
                continue;
            }

            // 本轮删了这个键。对方没改过，或也删了，就删；对方写成了新值则拒绝。
            if (!theirs.TryGetValue(key, out var theirValue)
                || string.Equals(theirValue, baseline[key], StringComparison.Ordinal))
            {
                slots.Remove(key);
                continue;
            }

            return Reject(key);
        }

        foreach (var (key, ourValue) in ours)
        {
            baseline.TryGetValue(key, out var baseValue);
            var weChanged = !baseline.ContainsKey(key)
                            || !string.Equals(baseValue, ourValue, StringComparison.Ordinal);
            if (!weChanged)
            {
                continue;
            }

            theirs.TryGetValue(key, out var theirValue);
            var theyChanged = !SamePresence(baseline, theirs, key)
                              || (baseline.ContainsKey(key)
                                  && !string.Equals(baseValue, theirValue, StringComparison.Ordinal));
            if (theyChanged && !string.Equals(theirValue, ourValue, StringComparison.Ordinal))
            {
                return Reject(key);
            }

            slots[key] = ourValue;
        }

        return new SessionMergeResult
        {
            Accepted = true,
            Slots = slots,
            Messages = MergeMessages(ourMessages, messageBaseline, theirMessages)
        };
    }

    private static SessionMergeResult Reject(string key) =>
        new() { Accepted = false, ConflictKey = key };

    private static bool SamePresence(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right,
        string key) =>
        left.ContainsKey(key) == right.ContainsKey(key);

    private static List<AgentMessage> MergeMessages(
        IReadOnlyList<AgentMessage> ourMessages,
        int messageBaseline,
        IReadOnlyList<AgentMessage> theirMessages)
    {
        var merged = theirMessages.ToList();
        if (messageBaseline < 0)
        {
            messageBaseline = 0;
        }

        if (messageBaseline > ourMessages.Count)
        {
            messageBaseline = ourMessages.Count;
        }

        var added = ourMessages.Skip(messageBaseline).ToList();
        if (added.Count > 0 && EndsWith(merged, added))
        {
            return merged;
        }

        merged.AddRange(added);
        return merged;
    }

    private static bool EndsWith(IReadOnlyList<AgentMessage> messages, IReadOnlyList<AgentMessage> suffix)
    {
        if (messages.Count < suffix.Count)
        {
            return false;
        }

        var start = messages.Count - suffix.Count;
        for (var i = 0; i < suffix.Count; i++)
        {
            if (!SameMessage(messages[start + i], suffix[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameMessage(AgentMessage left, AgentMessage right) =>
        string.Equals(left.Role, right.Role, StringComparison.Ordinal)
        && string.Equals(left.Content, right.Content, StringComparison.Ordinal)
        && string.Equals(left.Intent, right.Intent, StringComparison.Ordinal);
}
