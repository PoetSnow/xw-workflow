using System.Globalization;
using System.Text.Json;
using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Models;

namespace XW.Approve.Domain.Rules;

/// <summary>待办用哪份清单：解析器非空则覆盖节点默认。</summary>
public static class FormSchemaSelector
{
    /// <summary>返回这份待办生效的清单 JSON。没有则 null。</summary>
    public static string? ForTask(
        ProcessRuntimeGraph graph,
        long processNodeId,
        ApproveTask task,
        long submitUserId,
        AssigneeResolveContext org)
    {
        var node = graph.Nodes.FirstOrDefault(x => x.Id == processNodeId);
        if (node is null || !ApproveNodeType.HasForm(node.NodeType))
        {
            return null;
        }

        var rules = graph.Resolvers
            .Where(r => r.ProcessNodeId == processNodeId)
            .OrderBy(r => r.Ordinal)
            .ThenBy(r => r.Id);
        foreach (var rule in rules)
        {
            if (!Owns(rule, task, submitUserId, org))
            {
                continue;
            }

            return string.IsNullOrWhiteSpace(rule.FormSchema) ? BlankToNull(node.FormSchema) : rule.FormSchema;
        }

        return BlankToNull(node.FormSchema);
    }

    private static string? BlankToNull(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : json;

    private static bool Owns(ApproveResolver rule, ApproveTask task, long submitUserId, AssigneeResolveContext org)
    {
        var type = rule.ResolverType?.Trim() ?? string.Empty;
        if (ApproveToken.Equals(type, ApproveResolverType.Submitter))
        {
            return task.PositionId is null && task.AssigneeUserId == submitUserId;
        }

        if (ApproveToken.Equals(type, ApproveResolverType.User))
        {
            return task.PositionId is null
                   && long.TryParse(rule.ResolverValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId)
                   && task.AssigneeUserId == userId;
        }

        if (task.PositionId is not long positionId)
        {
            return false;
        }

        if (!TryPositionId(rule.ResolverValue, org, out var rulePosition) || rulePosition != positionId)
        {
            return false;
        }

        if (ApproveToken.Equals(type, ApproveResolverType.PositionInFixedDept))
        {
            return TryFixedDeptId(rule.ResolverExt, out var deptId) && task.DeptId == deptId;
        }

        return ApproveToken.Equals(type, ApproveResolverType.PositionFromDocDept);
    }

    private static bool TryPositionId(string? value, AssigneeResolveContext org, out long positionId)
    {
        var raw = value?.Trim() ?? string.Empty;
        if (org.PositionCodeToId.TryGetValue(raw, out positionId))
        {
            return true;
        }

        foreach (var pair in org.PositionCodeToId)
        {
            if (string.Equals(pair.Key, raw, StringComparison.OrdinalIgnoreCase))
            {
                positionId = pair.Value;
                return true;
            }
        }

        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out positionId);
    }

    private static bool TryFixedDeptId(string? resolverExt, out long deptId)
    {
        deptId = 0;
        if (string.IsNullOrWhiteSpace(resolverExt))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(resolverExt);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!ApproveToken.Equals(prop.Name, "DeptId"))
                {
                    continue;
                }

                var raw = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetRawText() : prop.Value.GetString();
                return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out deptId) && deptId > 0;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }
}
