using System.Text.Json.Serialization;

namespace System.Text.Json;

/// <summary>
/// long 序列化为 JSON 字符串，避免前端 Number 精度丢失（雪花 Id &gt; 2^53-1）。
/// </summary>
public sealed class Int64JsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => long.Parse(reader.GetString()!),
            JsonTokenType.Number => reader.GetInt64(),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing long."),
        };
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <inheritdoc cref="Int64JsonConverter"/>
public sealed class Int64NullableJsonConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return reader.TokenType switch
        {
            JsonTokenType.String => long.Parse(reader.GetString()!),
            JsonTokenType.Number => reader.GetInt64(),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing long?.")
        };
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString());
    }
}
