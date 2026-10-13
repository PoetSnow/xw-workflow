using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace XW.Infra.Core.Json;

/// <summary>
/// Provides a set of static methods and properties for JSON serialization and deserialization using System.Text.Json.
/// </summary>
public static class SystemTextJson
{
    /// <summary>
    /// Gets the default JsonSerializerOptions used by the  framework.
    /// </summary>
    /// <param name="configOptions">A callback function that configures additional JsonSerializerOptions.</param>
    /// <returns>The default JsonSerializerOptions used by the  framework.</returns>
    public static JsonSerializerOptions GetXWDefaultOptions(Action<JsonSerializerOptions>? configOptions = null)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DateTimeConverter());
        options.Converters.Add(new DateTimeNullableConverter());
        options.Encoder = GetXWDefaultEncoder();
        // This value indicates whether comments are allowed, disallowed, or skipped.
        options.ReadCommentHandling = JsonCommentHandling.Skip;
        // Settings for serializing dynamic types and anonymous types
        options.PropertyNameCaseInsensitive = true;
        // For dynamic types
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        // For anonymous types
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        configOptions?.Invoke(options);
        return options;
    }

    /// <summary>
    /// Gets the default JavaScriptEncoder used by the  framework.
    /// </summary>
    /// <returns>The default JavaScriptEncoder used by the  framework.</returns>
    public static JavaScriptEncoder GetXWDefaultEncoder() => JavaScriptEncoder.Create(new TextEncoderSettings(UnicodeRanges.All));
}
