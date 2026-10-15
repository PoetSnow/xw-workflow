using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Contracts.Knowledge;

namespace XW.Agent.Infrastructure.Knowledge;

/// <summary>上传后切块、向量化，写入调用方指定的资料集。</summary>
public sealed class KnowledgeIngestService(
    IDocumentTextExtractor extractor,
    IEmbeddingClient embedding,
    IKnowledgeVectorStore store,
    IHostEnvironment env,
    IOptionsMonitor<KnowledgeConnectionOptions> options,
    IOptionsMonitor<EmbeddingOptions> embeddingOptions,
    ILogger<KnowledgeIngestService> logger) : IKnowledgeIngest
{
    /// <inheritdoc />
    public async Task<KnowledgeIngestResult> IngestAsync(
        KnowledgeIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        var corpusId = request.CorpusId?.Trim() ?? string.Empty;
        if (corpusId.Length == 0)
        {
            throw new KnowledgeCorpusRequiredException();
        }

        var cfg = options.CurrentValue;
        if (!cfg.Enabled)
        {
            throw new InvalidOperationException("知识库已关闭（Agent:Knowledge:Enabled=false）。");
        }

        if (!embeddingOptions.CurrentValue.Enabled)
        {
            throw new InvalidOperationException("Embedding 已关闭（Agent:Knowledge:Embedding:Enabled=false）。");
        }

        if (!string.Equals(cfg.Provider, "vector", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("入库仅支持 Agent:Knowledge:Provider=vector。");
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
        await request.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > cfg.MaxUploadBytes)
        {
            throw new InvalidOperationException($"文件过大 {buffer.Length} > MaxUploadBytes={cfg.MaxUploadBytes}");
        }

        buffer.Position = 0;
        var documentId = string.IsNullOrWhiteSpace(request.DocumentId)
            ? Guid.NewGuid().ToString("N")
            : request.DocumentId.Trim();
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? Path.GetFileNameWithoutExtension(fileName)
            : request.Title.Trim();
        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType;

        string? storagePath = null;
        try
        {
            storagePath = await SaveOriginalAsync(buffer, documentId, fileName, cancellationToken);
            buffer.Position = 0;
            await store.UpsertDocumentAsync(new KnowledgeDocumentRecord
            {
                Id = documentId,
                Title = title,
                CorpusId = corpusId,
                SourceName = fileName,
                ContentType = contentType,
                StoragePath = storagePath,
                ByteSize = buffer.Length,
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            var text = await extractor.ExtractAsync(buffer, fileName, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("解析后文本为空，无法入库");
            }

            var pieces = KnowledgeTextChunker.Split(text, cfg.ChunkSize, cfg.ChunkOverlap);
            if (pieces.Count == 0)
            {
                throw new InvalidOperationException("切块结果为空");
            }

            var vectors = await embedding.EmbedBatchAsync(pieces.Select(piece => piece.Text).ToArray(), cancellationToken);
            var chunks = new List<KnowledgeChunkWriteModel>(pieces.Count);
            for (var i = 0; i < pieces.Count; i++)
            {
                var clauseNo = pieces[i].ClauseNo;
                chunks.Add(new KnowledgeChunkWriteModel
                {
                    Id = $"{documentId}#{i + 1}",
                    DocumentId = documentId,
                    ChunkIndex = i + 1,
                    CorpusId = corpusId,
                    Title = string.IsNullOrEmpty(clauseNo) ? title : $"{title} {clauseNo}",
                    SourceName = fileName,
                    Content = pieces[i].Text,
                    Embedding = vectors[i]
                });
            }

            await store.ReplaceChunksAsync(documentId, chunks, cancellationToken);
            await store.UpsertDocumentAsync(new KnowledgeDocumentRecord
            {
                Id = documentId,
                Title = title,
                CorpusId = corpusId,
                SourceName = fileName,
                ContentType = contentType,
                StoragePath = storagePath,
                ByteSize = buffer.Length,
                Status = "ready",
                CreatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            logger.LogInformation(
                "Knowledge ingested id={Id} file={File} chunks={Chunks} corpus={Corpus}",
                documentId,
                fileName,
                chunks.Count,
                corpusId);
            return new KnowledgeIngestResult
            {
                DocumentId = documentId,
                Title = title,
                CorpusId = corpusId,
                Status = "ready",
                ChunkCount = chunks.Count,
                StoragePath = storagePath
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Knowledge ingest failed file={File}", fileName);
            try
            {
                await store.UpsertDocumentAsync(new KnowledgeDocumentRecord
                {
                    Id = documentId,
                    Title = title,
                    CorpusId = corpusId,
                    SourceName = fileName,
                    ContentType = contentType,
                    StoragePath = storagePath,
                    ByteSize = buffer.Length,
                    Status = "failed",
                    ErrorMessage = Truncate(ex.Message, 500),
                    CreatedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
            }
            catch (Exception upsertEx)
            {
                logger.LogWarning(upsertEx, "Failed to mark document failed id={Id}", documentId);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(string corpusId, string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(corpusId))
        {
            throw new KnowledgeCorpusRequiredException();
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("documentId 无效");
        }

        return store.DeleteDocumentAsync(corpusId, documentId, cancellationToken);
    }

    private async Task<string> SaveOriginalAsync(
        MemoryStream buffer,
        string documentId,
        string fileName,
        CancellationToken cancellationToken)
    {
        var cfg = options.CurrentValue;
        var root = Path.Combine(env.ContentRootPath, cfg.UploadRelativePath, documentId);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, fileName);
        buffer.Position = 0;
        await using (var fs = File.Create(path))
        {
            await buffer.CopyToAsync(fs, cancellationToken);
        }

        return Path.Combine(cfg.UploadRelativePath, documentId, fileName).Replace('\\', '/');
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "...";
}
