using System.Globalization;
using System.Text.Json;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;

namespace XW.Approve.Domain.Rules;

/// <summary>按表单快照计算条件组。只读快照，不读业务表。</summary>
public static class ConditionEvaluator
{
    /// <summary>归属对象上的根条件组是否全部命中。没有条件组视为通过。</summary>
    public static bool Matches(
        long ownerId,
        string ownerType,
        IReadOnlyList<ApproveConditionGroup> groups,
        IReadOnlyList<ApproveConditionItem> items,
        string formSnapshot,
        long submitUserId,
        long submitDeptId)
    {
        var roots = groups
            .Where(g => ApproveToken.Equals(g.OwnerType, ownerType)
                        && g.OwnerId == ownerId
                        && g.ParentGroupId is null)
            .OrderBy(g => g.Ordinal)
            .ToList();

        if (roots.Count == 0)
        {
            return true;
        }

        JsonElement snapshot = ParseSnapshot(formSnapshot);
        return roots.All(root => EvalGroup(root, groups, items, snapshot, submitUserId, submitDeptId));
    }

    private static bool EvalGroup(
        ApproveConditionGroup group,
        IReadOnlyList<ApproveConditionGroup> groups,
        IReadOnlyList<ApproveConditionItem> items,
        JsonElement snapshot,
        long submitUserId,
        long submitDeptId)
    {
        var childGroups = groups
            .Where(g => g.ParentGroupId == group.Id)
            .OrderBy(g => g.Ordinal)
            .ToList();
        var childItems = items
            .Where(i => i.GroupId == group.Id)
            .OrderBy(i => i.Ordinal)
            .ToList();

        var results = new List<bool>(childGroups.Count + childItems.Count);
        results.AddRange(childGroups.Select(g => EvalGroup(g, groups, items, snapshot, submitUserId, submitDeptId)));
        results.AddRange(childItems.Select(i => EvalItem(i, snapshot, submitUserId, submitDeptId)));

        if (results.Count == 0)
        {
            return true;
        }

        return ApproveToken.Equals(group.GroupLogic, ApproveGroupLogic.Or)
            ? results.Any(x => x)
            : results.All(x => x);
    }

    private static bool EvalItem(ApproveConditionItem item, JsonElement snapshot, long submitUserId, long submitDeptId)
    {
        var left = ReadField(snapshot, item.FieldKey);
        var right = ResolveRight(item, snapshot, submitUserId, submitDeptId);
        var op = item.Operator?.Trim() ?? ApproveOperator.Equal;

        if (ApproveToken.Equals(op, ApproveOperator.Equal) || op == "==")
        {
            return Compare(left, right) == 0;
        }

        if (ApproveToken.Equals(op, ApproveOperator.NotEqual) || op == "<>")
        {
            return Compare(left, right) != 0;
        }

        if (ApproveToken.Equals(op, ApproveOperator.GreaterThan))
        {
            return Compare(left, right) > 0;
        }

        if (ApproveToken.Equals(op, ApproveOperator.GreaterThanOrEqual))
        {
            return Compare(left, right) >= 0;
        }

        if (ApproveToken.Equals(op, ApproveOperator.LessThan))
        {
            return Compare(left, right) < 0;
        }

        if (ApproveToken.Equals(op, ApproveOperator.LessThanOrEqual))
        {
            return Compare(left, right) <= 0;
        }

        if (ApproveToken.Equals(op, ApproveOperator.In))
        {
            return InList(left, right, not: false);
        }

        if (ApproveToken.Equals(op, ApproveOperator.NotIn))
        {
            return InList(left, right, not: true);
        }

        if (ApproveToken.Equals(op, ApproveOperator.Contains))
        {
            return (left ?? string.Empty).Contains(right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string? ResolveRight(ApproveConditionItem item, JsonElement snapshot, long submitUserId, long submitDeptId)
    {
        var type = item.ValueType ?? ApproveValueType.Const;
        if (ApproveToken.Equals(type, ApproveValueType.Field))
        {
            return ReadField(snapshot, item.Value);
        }

        if (ApproveToken.Equals(type, ApproveValueType.Initiator))
        {
            return submitUserId.ToString(CultureInfo.InvariantCulture);
        }

        if (ApproveToken.Equals(type, ApproveValueType.InitiatorDept))
        {
            return submitDeptId.ToString(CultureInfo.InvariantCulture);
        }

        return item.Value;
    }

    /// <summary>该出线是否配置了根条件组。无条件的非默认线不能当成命中。</summary>
    public static bool HasRootGroups(
        long ownerId,
        string ownerType,
        IReadOnlyList<ApproveConditionGroup> groups) =>
        groups.Any(g => ApproveToken.Equals(g.OwnerType, ownerType)
                        && g.OwnerId == ownerId
                        && g.ParentGroupId is null);

    /// <summary>按字段键读快照，忽略大小写和下划线。键来自 appr_business_field.FieldKey。</summary>
    internal static string? ReadField(JsonElement snapshot, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || snapshot.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var prop in snapshot.EnumerateObject())
        {
            if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop.Name.Replace("_", string.Empty), key.Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText(),
                };
            }
        }

        return null;
    }

    private static readonly JsonElement EmptySnapshot = CreateEmptySnapshot();

    /// <summary>解析表单快照 JSON，非法时按空对象处理。</summary>
    internal static JsonElement ParseSnapshot(string formSnapshot)
    {
        if (string.IsNullOrWhiteSpace(formSnapshot))
        {
            return EmptySnapshot.Clone();
        }

        try
        {
            using var doc = JsonDocument.Parse(formSnapshot);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return EmptySnapshot.Clone();
        }
    }

    private static JsonElement CreateEmptySnapshot()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    private static int Compare(string? left, string? right)
    {
        if (decimal.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var ln)
            && decimal.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var rn))
        {
            return ln.CompareTo(rn);
        }

        return string.Compare(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool InList(string? left, string? right, bool not)
    {
        var set = (right ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hit = set.Any(x => string.Equals(x, left, StringComparison.OrdinalIgnoreCase));
        return not ? !hit : hit;
    }
}
