using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Options;
using XW.Agent.Application.Telemetry;

namespace XW.Agent.Application.Knowledge;

/// <summary>知识文档入库请求（上传流）。</summary>
public sealed class KnowledgeIngestRequest
{
    /// <summary>文件内容流。</summary>
    public required Stream Content { get; init; }

    /// <summary>原始文件名。</summary>
    public required string FileName { get; init; }

    /// <summary>MIME 类型（可空）。</summary>
    public string? ContentType { get; init; }

    /// <summary>业务域（可空，默认 general）。</summary>
    public string? Domain { get; init; }

    /// <summary>标题（可空，默认取文件名）。</summary>
    public string? Title { get; init; }

    /// <summary>指定文档 Id（可空则自动生成）。</summary>
    public string? DocumentId { get; init; }
}

/// <summary>知识入库结果。</summary>
public sealed class KnowledgeIngestResult
{
    public required string DocumentId { get; init; }
    public required string Title { get; init; }
    public required string Domain { get; init; }
    /// <summary>ready | failed | …</summary>
    public required string Status { get; init; }
    public int ChunkCount { get; init; }
    public string? StoragePath { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>知识入库服务：解析 → 切块 → Embedding → 向量库。</summary>
public interface IKnowledgeIngestService
{
    /// <summary>上传并入库一篇文档。</summary>
    Task<KnowledgeIngestResult> IngestAsync(KnowledgeIngestRequest request, CancellationToken ct = default);

    /// <summary>按文档 Id 删除。</summary>
    Task DeleteAsync(string documentId, CancellationToken ct = default);
}

/// <summary>上传 → 解析文本 → 切块 → Embedding HTTP → pgvector 落库。不维护词库。</summary>
public sealed class KnowledgeIngestService(
    IDocumentTextExtractor extractor,
    IEmbeddingClient embedding,
    IKnowledgeVectorStore store,
    IHostEnvironment env,
    IOptionsMonitor<AgentOptions> options,
    ILogger<KnowledgeIngestService> logger) : IKnowledgeIngestService
{
    /// <inheritdoc />
    public async Task<KnowledgeIngestResult> IngestAsync(KnowledgeIngestRequest request, CancellationToken ct = default)
    {
        using var activity = AgentTelemetry.StartState("rag.ingest");
        var cfg = options.CurrentValue.Knowledge;
        if (!cfg.Enabled)
        {
            throw new InvalidOperationException(
                "知识库已关闭（Agent:Knowledge:Enabled=false）。开启后再上传入库。");
        }

        if (!cfg.Embedding.Enabled)
        {
            throw new InvalidOperationException(
                "Embedding 已关闭（Agent:Knowledge:Embedding:Enabled=false）。向量入库需要 Embedding 服务，不会回退词库。");
        }

        if (!string.Equals(cfg.Provider, "vector", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "入库仅支持 Agent:Knowledge:Provider=vector。");
        }

        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        var fileName = Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("FileName 无效");
        }

        await using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, ct);
        if (buffer.Length > cfg.MaxUploadBytes)
        {
            throw new InvalidOperationException(
                $"文件过大 {buffer.Length} > MaxUploadBytes={cfg.MaxUploadBytes}");
        }

        buffer.Position = 0;
        var documentId = string.IsNullOrWhiteSpace(request.DocumentId)
            ? Guid.NewGuid().ToString("N")
            : request.DocumentId.Trim();
        var domain = string.IsNullOrWhiteSpace(request.Domain) ? "general" : request.Domain.Trim();
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? Path.GetFileNameWithoutExtension(fileName)
            : request.Title.Trim();
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType!;

        string? storagePath = null;
        try
        {
            storagePath = await SaveOriginalAsync(buffer, documentId, fileName, ct);
            buffer.Position = 0;

            await store.UpsertDocumentAsync(new KnowledgeDocumentRecord
            {
                Id = documentId,
                Title = title,
                Domain = domain,
                SourceName = fileName,
                ContentType = contentType,
                StoragePath = storagePath,
                ByteSize = buffer.Length,
                Status = "pending",
                ErrorMessage = null,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);

            var text = await extractor.ExtractAsync(buffer, fileName, ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("解析后文本为空，无法入库");
            }

            // 正文 frontmatter「domain: aftersales」优先于默认 general
            var domainFromText = TryParseDomainLine(text);
            if (!string.IsNullOrWhiteSpace(domainFromText)
                && (string.IsNullOrWhiteSpace(request.Domain)
                    || string.Equals(domain, "general", StringComparison.OrdinalIgnoreCase)))
            {
                domain = domainFromText!;
            }

            var pieces = KnowledgeTextChunker.Chunk(text, cfg.ChunkSize, cfg.ChunkOverlap);
            if (pieces.Count == 0)
            {
                throw new InvalidOperationException("切块结果为空");
            }

            var vectors = await embedding.EmbedBatchAsync(pieces, ct);
            var chunks = new List<KnowledgeChunkWriteModel>(pieces.Count);
            for (var i = 0; i < pieces.Count; i++)
            {
                chunks.Add(new KnowledgeChunkWriteModel
                {
                    Id = $"{documentId}#{i + 1}",
                    DocumentId = documentId,
                    ChunkIndex = i + 1,
                    Domain = domain,
                    Title = title,
                    SourceName = fileName,
                    Content = pieces[i],
                    Embedding = vectors[i]
                });
            }

            await store.ReplaceChunksAsync(documentId, chunks, ct);
            await store.UpsertDocumentAsync(new KnowledgeDocumentRecord
            {
                Id = documentId,
                Title = title,
                Domain = domain,
                SourceName = fileName,
                ContentType = contentType,
                StoragePath = storagePath,
                ByteSize = buffer.Length,
                Status = "ready",
                ErrorMessage = null,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);

            activity?.SetTag("agent.rag.doc_id", documentId);
            activity?.SetTag("agent.rag.chunks", chunks.Count);
            AgentTelemetry.SetOk(activity, true);

            logger.LogInformation(
                "Knowledge ingested id={Id} file={File} chunks={Chunks} domain={Domain}",
                documentId, fileName, chunks.Count, domain);

            return new KnowledgeIngestResult
            {
                DocumentId = documentId,
                Title = title,
                Domain = domain,
                Status = "ready",
                ChunkCount = chunks.Count,
                StoragePath = storagePath
            };
        }
        catch (Exception ex)
        {
            AgentTelemetry.SetError(activity, ex);
            logger.LogError(ex, "Knowledge ingest failed file={File}", fileName);
            try
            {
                await store.UpsertDocumentAsync(new KnowledgeDocumentRecord
                {
                    Id = documentId,
                    Title = title,
                    Domain = domain,
                    SourceName = fileName,
                    ContentType = contentType,
                    StoragePath = storagePath,
                    ByteSize = buffer.Length,
                    Status = "failed",
                    ErrorMessage = Truncate(ex.Message, 500),
                    CreatedAt = DateTimeOffset.UtcNow
                }, ct);
            }
            catch (Exception upsertEx)
            {
                logger.LogWarning(upsertEx, "Failed to mark document failed id={Id}", documentId);
            }

            throw;
        }
    }

    public Task DeleteAsync(string documentId, CancellationToken ct = default) =>
        store.DeleteDocumentAsync(documentId, ct);

    private async Task<string> SaveOriginalAsync(
        MemoryStream buffer,
        string documentId,
        string fileName,
        CancellationToken ct)
    {
        var cfg = options.CurrentValue.Knowledge;
        var root = Path.Combine(env.ContentRootPath, cfg.UploadRelativePath, documentId);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, fileName);
        buffer.Position = 0;
        await using (var fs = File.Create(path))
        {
            await buffer.CopyToAsync(fs, ct);
        }

        return Path.Combine(cfg.UploadRelativePath, documentId, fileName).Replace('\\', '/');
    }

    private static string? TryParseDomainLine(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            text, @"^domain:\s*(\S+)\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline
            | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Length <= max ? s : s[..max] + "...";
}
