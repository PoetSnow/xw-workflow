using System.Globalization;
using System.Text.Json.Nodes;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Exceptions;

namespace XW.Approve.Domain.Rules;

/// <summary>用业务表列值按 FieldKey 写条件快照。键是列名，不是实体属性名。</summary>
public static class FormSnapshotBinder
{
    /// <summary>columns 的键必须等于 appr_business_field.FieldKey。</summary>
    public static string Bind(
        IReadOnlyDictionary<string, object?> columns,
        IReadOnlyList<ApproveBusinessField> fields,
        long submitDeptId)
    {
        columns ??= new Dictionary<string, object?>();
        var output = new JsonObject();

        foreach (var field in (fields ?? []).Where(x => x.Enabled).OrderBy(x => x.Ordinal))
        {
            var key = field.FieldKey?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = ColumnValue(columns, key) ?? SystemValue(field, key, submitDeptId);
            if (value is not null)
            {
                output[key] = value;
                continue;
            }

            if (field.Required)
            {
                var name = string.IsNullOrWhiteSpace(field.FieldName) ? key : field.FieldName;
                throw new ApprovalDomainException($"条件字段 {name}（{key}）在业务表中没有值");
            }
        }

        if (!HasKey(output, "DeptId"))
        {
            output["DeptId"] = submitDeptId;
        }

        output["SubmitDeptId"] = submitDeptId;
        return output.ToJsonString();
    }

    private static JsonNode? SystemValue(ApproveBusinessField field, string key, long submitDeptId)
    {
        if (ApproveToken.Equals(field.ValueSource, ApproveValueSource.System)
            || ApproveToken.Equals(field.FieldType, ApproveFieldType.Department)
            || string.Equals(key, "DeptId", StringComparison.OrdinalIgnoreCase))
        {
            return JsonValue.Create(submitDeptId);
        }

        return null;
    }

    private static JsonNode? ColumnValue(IReadOnlyDictionary<string, object?> columns, string fieldKey)
    {
        if (!columns.TryGetValue(fieldKey, out var value)
            && columns.Keys.FirstOrDefault(k => string.Equals(k, fieldKey, StringComparison.OrdinalIgnoreCase)) is { } actual)
        {
            value = columns[actual];
        }

        return ToNode(value);
    }

    private static JsonNode? ToNode(object? value)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        return value switch
        {
            JsonNode node => node.DeepClone(),
            bool flag => JsonValue.Create(flag),
            byte or sbyte or short or ushort or int or uint or long or ulong => JsonValue.Create(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            float or double or decimal => JsonValue.Create(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
            DateTime time => JsonValue.Create(time),
            string text => JsonValue.Create(text),
            _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
        };
    }

    private static bool HasKey(JsonObject obj, string key)
    {
        foreach (var pair in obj)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
                && pair.Value is not null
                && pair.Value.GetValueKind() is not System.Text.Json.JsonValueKind.Null)
            {
                return true;
            }
        }

        return false;
    }
}
