using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using XW.Inspection.Domain;

namespace XW.Inspection.Infrastructure.Agent;

/// <summary>Agent 宿主的 HTTP 客户端。</summary>
public sealed class AgentInspectionClient(HttpClient http, IConfiguration configuration) : IAgentInspectionClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public async Task<AgentIngestedDocument> IngestAsync(
        Stream content,
        string fileName,
        string corpusId,
        string title,
        CancellationToken cancellationToken = default)
    {
        EnsureBaseUrl();
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(content), "file", fileName);
        form.Add(new StringContent(corpusId), "corpus");
        form.Add(new StringContent(title), "title");
        using var response = await http.PostAsync("api/agent/knowledge/upload", form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ReadMessage(body) ?? "法规入库失败。");
        }

        return JsonSerializer.Deserialize<AgentIngestedDocument>(body, Json)
               ?? throw new InvalidOperationException("法规入库结果无法解析。");
    }

    /// <inheritdoc />
    public async Task<AgentHit?> SearchScopeAsync(string corpusId, CancellationToken cancellationToken = default)
    {
        EnsureBaseUrl();
        var url = "api/agent/knowledge/search?q=" + Uri.EscapeDataString("适用范围")
                  + "&corpus=" + Uri.EscapeDataString(corpusId);
        using var response = await http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<SearchBody>(body, Json);
        return parsed?.Hits.FirstOrDefault(item =>
                   (item.Title + item.Snippet).Contains("适用范围", StringComparison.Ordinal))
               ?? parsed?.Hits.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentListedItem>> ListItemsAsync(
        AgentListItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureBaseUrl();
        using var response = await http.PostAsJsonAsync("api/agent/jobs", new
        {
            jobId = request.JobId,
            kind = "inspection.list-items",
            corpusIds = request.CorpusIds,
            input = new
            {
                industry = request.Industry,
                projectType = request.ProjectType,
                medium = request.Medium,
                buildKind = request.BuildKind
            }
        }, Json, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ReadMessage(body) ?? "列检测项失败。");
        }

        var parsed = JsonSerializer.Deserialize<JobBody>(body, Json)
                     ?? throw new InvalidOperationException("列检测项结果无法解析。");
        if (!string.Equals(parsed.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(parsed.Error) ? "列检测项失败。" : parsed.Error);
        }

        return parsed.Output?.Items ?? [];
    }

    private void EnsureBaseUrl()
    {
        if (http.BaseAddress is not null)
        {
            return;
        }

        var configured = configuration["Inspection:AgentBaseUrl"];
        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(configured)
                ? "未配置 Inspection:AgentBaseUrl。"
                : "Inspection:AgentBaseUrl 无效。");
    }

    private static string? ReadMessage(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private sealed class SearchBody
    {
        public List<AgentHit> Hits { get; set; } = [];
    }

    private sealed class JobBody
    {
        public string Status { get; set; } = string.Empty;

        public string? Error { get; set; }

        public JobOutput? Output { get; set; }
    }

    private sealed class JobOutput
    {
        public List<AgentListedItem> Items { get; set; } = [];
    }
}
