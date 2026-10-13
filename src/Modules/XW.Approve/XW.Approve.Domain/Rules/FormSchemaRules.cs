using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Exceptions;
using XW.Approve.Domain.Models;

namespace XW.Approve.Domain.Rules;

/// <summary>交付清单的形状和提交值。条件字段不走这里。</summary>
public static class FormSchemaRules
{
    /// <summary>payload 最大字符数。</summary>
    public const int MaxPayloadLength = 65536;

    private static readonly Regex KeyPattern = new("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>没有字段、也不要附件。</summary>
    public static bool IsBlank(ApproveFormSchema? schema) =>
        schema is null
        || (schema.Fields.Count == 0 && !schema.AllowAttachment && !schema.AttachmentRequired);

    /// <summary>设计器保存。空清单返回 null。有问题写入 errors。</summary>
    public static string? TryCanonicalize(ApproveFormSchema? schema, string label, ICollection<string> errors)
    {
        if (IsBlank(schema))
        {
            return null;
        }

        var before = errors.Count;
        CollectShapeErrors(schema!, label, errors);
        return errors.Count == before
            ? JsonSerializer.Serialize(Normalize(schema!), WriteOptions)
            : null;
    }

    /// <summary>读已存清单。坏 JSON 当没有清单。</summary>
    public static ApproveFormSchema? TryRead(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ApproveFormSchema>(json, ReadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>是否允许上传附件。</summary>
    public static bool AllowsAttachment(string? schemaJson) =>
        TryRead(schemaJson) is { AllowAttachment: true };

    /// <summary>校验提交值，返回要写入 payload 的 JSON。</summary>
    public static string NormalizePayload(string? schemaJson, string? payloadJson, int attachmentCount)
    {
        if (!string.IsNullOrWhiteSpace(payloadJson) && payloadJson.Length > MaxPayloadLength)
        {
            throw new ApprovalDomainException("交付内容过长");
        }

        var schema = TryRead(schemaJson);
        if (!string.IsNullOrWhiteSpace(schemaJson) && schema is null)
        {
            throw new ApprovalDomainException("交付清单无法读取");
        }

        if (IsBlank(schema))
        {
            if (attachmentCount > 0)
            {
                throw new ApprovalDomainException("本环节不接收附件");
            }

            if (HasValues(payloadJson))
            {
                throw new ApprovalDomainException("本环节没有要填写的字段");
            }

            return "{}";
        }

        var shapeErrors = new List<string>();
        CollectShapeErrors(schema!, "交付清单", shapeErrors);
        if (shapeErrors.Count > 0)
        {
            throw new ApprovalDomainException(shapeErrors[0]);
        }

        if (!schema!.AllowAttachment && attachmentCount > 0)
        {
            throw new ApprovalDomainException("本环节不接收附件");
        }

        if (schema.AttachmentRequired && attachmentCount < 1)
        {
            throw new ApprovalDomainException("请至少上传一个附件");
        }

        using var doc = ParseObject(payloadJson);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var field = schema.Fields.FirstOrDefault(x => ApproveToken.Equals(x.Key, prop.Name));
            if (field is null)
            {
                throw new ApprovalDomainException($"字段 {prop.Name} 不在本环节清单里");
            }

            if (!seen.Add(field.Key))
            {
                throw new ApprovalDomainException($"字段 {field.Name} 重复提交");
            }

            var read = ReadValue(field, prop.Value);
            if (read is not null)
            {
                output[field.Key] = read;
            }
        }

        foreach (var field in schema.Fields.Where(x => x.Required))
        {
            if (!output.TryGetValue(field.Key, out var value) || value is null || value is string text && text.Length == 0)
            {
                throw new ApprovalDomainException($"{field.Name} 必填");
            }
        }

        return JsonSerializer.Serialize(output, WriteOptions);
    }

    private static void CollectShapeErrors(ApproveFormSchema schema, string label, ICollection<string> errors)
    {
        if (schema.AttachmentRequired && !schema.AllowAttachment)
        {
            errors.Add($"{label} 要求附件时必须允许上传");
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in schema.Fields)
        {
            var key = field.Key?.Trim() ?? string.Empty;
            var name = field.Name?.Trim() ?? string.Empty;
            var type = CanonicalType(field.Type);
            if (!KeyPattern.IsMatch(key))
            {
                errors.Add($"{label} 的字段键不合法");
                continue;
            }

            if (!keys.Add(key))
            {
                errors.Add($"{label} 的字段 {key} 重复");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"{label} 的字段 {key} 缺少名称");
            }

            if (type is null)
            {
                errors.Add($"{label} 的字段 {key} 类型不支持");
                continue;
            }

            if (ApproveToken.Equals(type, ApproveFormFieldType.Enum))
            {
                var options = CleanOptions(field.Options);
                if (options.Count == 0)
                {
                    errors.Add($"{label} 的字段 {name} 缺少选项");
                }
            }
        }
    }

    private static ApproveFormSchema Normalize(ApproveFormSchema schema) =>
        new()
        {
            AllowAttachment = schema.AllowAttachment,
            AttachmentRequired = schema.AllowAttachment && schema.AttachmentRequired,
            Fields = schema.Fields.Select(field =>
            {
                var type = CanonicalType(field.Type)!;
                return new ApproveFormField
                {
                    Key = field.Key.Trim(),
                    Name = field.Name.Trim(),
                    Type = type,
                    Required = field.Required,
                    Options = ApproveToken.Equals(type, ApproveFormFieldType.Enum) ? CleanOptions(field.Options) : [],
                };
            }).ToList(),
        };

    private static List<string> CleanOptions(IEnumerable<string>? options)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options ?? [])
        {
            var text = option?.Trim() ?? string.Empty;
            if (text.Length == 0 || text.Length > 64 || !seen.Add(text))
            {
                continue;
            }

            list.Add(text);
        }

        return list;
    }

    private static string? CanonicalType(string? type)
    {
        if (ApproveToken.Equals(type, ApproveFormFieldType.String))
        {
            return ApproveFormFieldType.String;
        }

        if (ApproveToken.Equals(type, ApproveFormFieldType.Text))
        {
            return ApproveFormFieldType.Text;
        }

        if (ApproveToken.Equals(type, ApproveFormFieldType.Number))
        {
            return ApproveFormFieldType.Number;
        }

        if (ApproveToken.Equals(type, ApproveFormFieldType.Date))
        {
            return ApproveFormFieldType.Date;
        }

        if (ApproveToken.Equals(type, ApproveFormFieldType.Enum))
        {
            return ApproveFormFieldType.Enum;
        }

        return null;
    }

    private static bool HasValues(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.EnumerateObject().Any();
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static JsonDocument ParseObject(string? payloadJson)
    {
        var raw = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        try
        {
            var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                doc.Dispose();
                throw new ApprovalDomainException("交付内容必须是一组字段");
            }

            return doc;
        }
        catch (JsonException)
        {
            throw new ApprovalDomainException("交付内容不是合法 JSON");
        }
    }

    private static object? ReadValue(ApproveFormField field, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            if (field.Required)
            {
                throw new ApprovalDomainException($"{field.Name} 必填");
            }

            return null;
        }

        if (ApproveToken.Equals(field.Type, ApproveFormFieldType.Number))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String
                && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }

            throw new ApprovalDomainException($"{field.Name} 须为数字");
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ApprovalDomainException($"{field.Name} 须为文本");
        }

        var text = value.GetString()?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            if (field.Required)
            {
                throw new ApprovalDomainException($"{field.Name} 必填");
            }

            return null;
        }

        if (ApproveToken.Equals(field.Type, ApproveFormFieldType.String) && text.Length > 500)
        {
            throw new ApprovalDomainException($"{field.Name} 过长");
        }

        if (ApproveToken.Equals(field.Type, ApproveFormFieldType.Text) && text.Length > 2000)
        {
            throw new ApprovalDomainException($"{field.Name} 过长");
        }

        if (ApproveToken.Equals(field.Type, ApproveFormFieldType.Date)
            && !DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new ApprovalDomainException($"{field.Name} 须为日期");
        }

        if (ApproveToken.Equals(field.Type, ApproveFormFieldType.Enum)
            && !field.Options.Contains(text, StringComparer.Ordinal))
        {
            throw new ApprovalDomainException($"{field.Name} 不在选项里");
        }

        return text;
    }
}
