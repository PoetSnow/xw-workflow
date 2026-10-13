using System.Globalization;
using System.Text.Json;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Exceptions;
using XW.Approve.Domain.Models;

namespace XW.Approve.Domain.Rules;

/// <summary>按节点解析器找人。岗位走任职和部门树。</summary>
public static class AssigneeResolver
{
    /// <summary>解析本节点审批人。允许自己批自己。</summary>
    public static IReadOnlyList<ResolvedAssignee> Resolve(
        ApproveProcessNode node,
        ProcessRuntimeGraph graph,
        AssigneeResolveContext org,
        string formSnapshot,
        long submitUserId,
        long submitDeptId)
    {
        var rules = graph.Resolvers
            .Where(r => r.ProcessNodeId == node.Id)
            .OrderBy(r => r.Ordinal)
            .ToList();

        if (rules.Count == 0)
        {
            return [];
        }

        var result = new List<ResolvedAssignee>();
        var seenUsers = new HashSet<long>();

        foreach (var rule in rules)
        {
            foreach (var person in ResolveRule(rule, org, formSnapshot, submitUserId, submitDeptId))
            {
                if (person.UserId is long uid)
                {
                    if (seenUsers.Add(uid))
                    {
                        result.Add(person);
                    }
                }
                else
                {
                    result.Add(person);
                }
            }
        }

        return result;
    }

    private static IEnumerable<ResolvedAssignee> ResolveRule(
        ApproveResolver rule,
        AssigneeResolveContext org,
        string formSnapshot,
        long submitUserId,
        long submitDeptId)
    {
        var type = rule.ResolverType?.Trim() ?? string.Empty;
        if (ApproveToken.Equals(type, ApproveResolverType.Submitter))
        {
            yield return ResolvedAssignee.ForUser(submitUserId, deptId: submitDeptId);
            yield break;
        }

        if (ApproveToken.Equals(type, ApproveResolverType.User))
        {
            if (!long.TryParse(rule.ResolverValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
            {
                throw new ApprovalDomainException($"User 解析器无法识别用户 Id：{rule.ResolverValue}");
            }

            yield return ResolvedAssignee.ForUser(userId);
            yield break;
        }

        if (ApproveToken.Equals(type, ApproveResolverType.PositionFromDocDept))
        {
            var positionId = RequirePosition(rule.ResolverValue, org);
            var deptId = ReadSnapshotDeptId(formSnapshot);
            foreach (var person in FindByPositionWalkingUp(org, positionId, deptId, rule.ResolverValue))
            {
                yield return person;
            }

            yield break;
        }

        if (ApproveToken.Equals(type, ApproveResolverType.PositionInFixedDept))
        {
            var positionId = RequirePosition(rule.ResolverValue, org);
            var deptId = ReadFixedDeptId(rule.ResolverExt);
            var hits = org.Appointments
                .Where(a => a.PositionId == positionId && a.DeptId == deptId)
                .Select(a => ResolvedAssignee.ForUser(a.UserId, a.PositionId, a.DeptId))
                .ToList();
            if (hits.Count == 0)
            {
                throw new ApprovalDomainException($"固定部门 {deptId} 上没有岗位 {rule.ResolverValue} 的任职");
            }

            foreach (var person in hits)
            {
                yield return person;
            }

            yield break;
        }

        throw new ApprovalDomainException($"不支持的解析器类型 {rule.ResolverType}");
    }

    private static IEnumerable<ResolvedAssignee> FindByPositionWalkingUp(
        AssigneeResolveContext org,
        long positionId,
        long startDeptId,
        string positionLabel)
    {
        var current = startDeptId;
        var seen = new HashSet<long>();
        while (current > 0 && seen.Add(current))
        {
            var hits = org.Appointments
                .Where(a => a.PositionId == positionId && a.DeptId == current)
                .Select(a => ResolvedAssignee.ForUser(a.UserId, a.PositionId, a.DeptId))
                .ToList();
            if (hits.Count > 0)
            {
                return hits;
            }

            if (!org.DeptParentMap.TryGetValue(current, out var parent) || parent == current)
            {
                break;
            }

            current = parent;
        }

        throw new ApprovalDomainException($"从部门 {startDeptId} 往上找不到岗位 {positionLabel} 的任职");
    }

    private static long RequirePosition(string? value, AssigneeResolveContext org)
    {
        var raw = value?.Trim() ?? string.Empty;
        if (org.PositionCodeToId.TryGetValue(raw, out var id))
        {
            return id;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
            && org.Appointments.Any(a => a.PositionId == id))
        {
            return id;
        }

        foreach (var pair in org.PositionCodeToId)
        {
            if (string.Equals(pair.Key, raw, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        throw new ApprovalDomainException($"找不到岗位 {value}");
    }

    private static long ReadSnapshotDeptId(string formSnapshot)
    {
        var snapshot = ConditionEvaluator.ParseSnapshot(formSnapshot);
        var raw = ConditionEvaluator.ReadField(snapshot, "DeptId")
                  ?? ConditionEvaluator.ReadField(snapshot, "dept_id")
                  ?? ConditionEvaluator.ReadField(snapshot, "deptid");
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deptId) || deptId <= 0)
        {
            throw new ApprovalDomainException("岗位解析需要快照中的 DeptId（单据审批部门）");
        }

        return deptId;
    }

    private static long ReadFixedDeptId(string? resolverExt)
    {
        if (string.IsNullOrWhiteSpace(resolverExt))
        {
            throw new ApprovalDomainException("PositionInFixedDept 必须指定固定部门");
        }

        try
        {
            using var doc = JsonDocument.Parse(resolverExt);
            if (TryGetIgnoreCase(doc.RootElement, "DeptId", out var p)
                && p.ValueKind is JsonValueKind.Number or JsonValueKind.String
                && long.TryParse(p.ValueKind == JsonValueKind.Number ? p.GetRawText() : p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var deptId)
                && deptId > 0)
            {
                return deptId;
            }
        }
        catch (JsonException)
        {
        }

        throw new ApprovalDomainException("PositionInFixedDept 必须指定固定部门");
    }

    private static bool TryGetIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (ApproveToken.Equals(prop.Name, name))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
