using System.Text.Json;
using System.Text.RegularExpressions;
using XW.Agent.Contracts.Jobs;
using XW.Agent.Contracts.Knowledge;

namespace XW.Agent.Inspection;

/// <summary>在已经选定的资料集里，把检索到的条款整理成检测项。</summary>
public sealed class ListInspectionItemsHandler(IKnowledgeRetriever retriever) : IJobHandler
{
    /// <summary>作业种类。</summary>
    public const string KindName = "inspection.list-items";

    /// <inheritdoc />
    public string Kind => KindName;

    /// <inheritdoc />
    public async Task<JobResult> HandleAsync(JobRequest request, CancellationToken cancellationToken = default)
    {
        var corpusIds = request.CorpusIds
            .Select(id => id?.Trim() ?? string.Empty)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (corpusIds.Length == 0)
        {
            return Failed(request.JobId, "缺少资料集编号。");
        }

        var industry = ReadString(request.Input, "industry");
        var projectType = ReadString(request.Input, "projectType");
        var medium = ReadString(request.Input, "medium");
        var buildKind = ReadString(request.Input, "buildKind");
        var query = string.Join(' ', new[] { industry, projectType, medium, buildKind, "检测项目 应当" }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        var searched = await retriever.SearchAsync(new KnowledgeSearchRequest
        {
            Query = query,
            CorpusIds = corpusIds,
            TopK = 16
        }, cancellationToken);
        if (searched.Rejected)
        {
            return Failed(request.JobId, searched.Reason ?? "缺少资料集编号。");
        }

        var items = searched.Hits.Select(hit =>
        {
            var clauseNo = ReadClause(hit);
            return new
            {
                name = string.IsNullOrWhiteSpace(hit.Title) ? clauseNo ?? "检测项" : hit.Title,
                requirement = hit.Snippet,
                basis = new
                {
                    corpusId = hit.CorpusId,
                    documentId = hit.DocumentId,
                    chunkId = hit.ChunkId,
                    clauseNo,
                    snippet = hit.Snippet
                }
            };
        }).ToArray();

        return new JobResult
        {
            JobId = request.JobId,
            Status = JobResult.Succeeded,
            Hits = searched.Hits,
            Output = JsonSerializer.SerializeToElement(new { items })
        };
    }

    private static JobResult Failed(string jobId, string error) =>
        new()
        {
            JobId = jobId,
            Status = JobResult.Failed,
            Error = error
        };

    private static string ReadString(JsonElement input, string name)
    {
        if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? string.Empty : string.Empty;
    }

    private static string? ReadClause(KnowledgeHit hit)
    {
        var match = Regex.Match($"{hit.Title}\n{hit.Snippet}", @"第[0-9０-９一二三四五六七八九十百零〇千]+条");
        return match.Success ? match.Value : null;
    }
}
