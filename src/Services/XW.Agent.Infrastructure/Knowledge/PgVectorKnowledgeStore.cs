using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using XW.Agent.Contracts.Knowledge;

namespace XW.Agent.Infrastructure.Knowledge;

/// <summary>向量库中的文档记录。</summary>
public sealed class KnowledgeDocumentRecord
{
    /// <summary>文档 Id。</summary>
    public required string Id { get; init; }

    /// <summary>标题。</summary>
    public required string Title { get; init; }

    /// <summary>资料集编号，写入 domain 列。</summary>
    public required string CorpusId { get; init; }

    /// <summary>来源文件名。</summary>
    public required string SourceName { get; init; }

    /// <summary>MIME 类型。</summary>
    public required string ContentType { get; init; }

    /// <summary>原件路径。</summary>
    public string? StoragePath { get; init; }

    /// <summary>字节数。</summary>
    public long ByteSize { get; init; }

    /// <summary>pending、ready 或 failed。</summary>
    public required string Status { get; init; }

    /// <summary>失败原因。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>切块数。</summary>
    public int ChunkCount { get; init; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>待写入的切块。</summary>
public sealed class KnowledgeChunkWriteModel
{
    /// <summary>切块 Id。</summary>
    public required string Id { get; init; }

    /// <summary>文档 Id。</summary>
    public required string DocumentId { get; init; }

    /// <summary>切块序号。</summary>
    public int ChunkIndex { get; init; }

    /// <summary>资料集编号。</summary>
    public required string CorpusId { get; init; }

    /// <summary>标题。</summary>
    public required string Title { get; init; }

    /// <summary>来源文件名。</summary>
    public required string SourceName { get; init; }

    /// <summary>切块正文。</summary>
    public required string Content { get; init; }

    /// <summary>向量。</summary>
    public required float[] Embedding { get; init; }
}

/// <summary>pgvector 上的文档与近邻检索。</summary>
public interface IKnowledgeVectorStore
{
    /// <summary>写入或更新文档。</summary>
    Task UpsertDocumentAsync(KnowledgeDocumentRecord document, CancellationToken cancellationToken = default);

    /// <summary>替换一篇文档的全部切块。</summary>
    Task ReplaceChunksAsync(
        string documentId,
        IReadOnlyList<KnowledgeChunkWriteModel> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>删除指定资料集中的文档和切块。</summary>
    Task DeleteDocumentAsync(
        string corpusId,
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>只在给定资料集里做近邻检索。列表为空时拒绝，不发查询。</summary>
    Task<IReadOnlyList<KnowledgeHit>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        IReadOnlyList<string> corpusIds,
        double minSimilarity,
        CancellationToken cancellationToken = default);
}

/// <summary>PostgreSQL + pgvector。资料集编号存在 domain 列。</summary>
public sealed class PgVectorKnowledgeStore(
    KnowledgeVectorDataSource dataSource,
    IOptionsMonitor<KnowledgeConnectionOptions> options,
    ILogger<PgVectorKnowledgeStore> logger) : IKnowledgeVectorStore
{
    /// <inheritdoc />
    public async Task UpsertDocumentAsync(KnowledgeDocumentRecord document, CancellationToken cancellationToken = default)
    {
        RequireConnectionString();
        const string sql = """
            INSERT INTO agent_knowledge_document
                (id, title, domain, source_name, content_type, storage_path, byte_size, status, error_message, created_at, updated_at)
            VALUES
                (@id, @title, @domain, @source, @ctype, @path, @size, @status, @err, now(), now())
            ON CONFLICT (id) DO UPDATE SET
                title = EXCLUDED.title,
                domain = EXCLUDED.domain,
                source_name = EXCLUDED.source_name,
                content_type = EXCLUDED.content_type,
                storage_path = EXCLUDED.storage_path,
                byte_size = EXCLUDED.byte_size,
                status = EXCLUDED.status,
                error_message = EXCLUDED.error_message,
                updated_at = now();
            """;

        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", document.Id);
        cmd.Parameters.AddWithValue("title", document.Title);
        cmd.Parameters.AddWithValue("domain", document.CorpusId);
        cmd.Parameters.AddWithValue("source", document.SourceName);
        cmd.Parameters.AddWithValue("ctype", document.ContentType);
        cmd.Parameters.AddWithValue("path", (object?)document.StoragePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("size", document.ByteSize);
        cmd.Parameters.AddWithValue("status", document.Status);
        cmd.Parameters.AddWithValue("err", (object?)document.ErrorMessage ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReplaceChunksAsync(
        string documentId,
        IReadOnlyList<KnowledgeChunkWriteModel> chunks,
        CancellationToken cancellationToken = default)
    {
        RequireConnectionString();
        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await using (var del = new NpgsqlCommand(
                         "DELETE FROM agent_knowledge_chunk WHERE document_id = @id",
                         conn,
                         (NpgsqlTransaction)tx))
        {
            del.Parameters.AddWithValue("id", documentId);
            await del.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insert = """
            INSERT INTO agent_knowledge_chunk
                (id, document_id, chunk_index, domain, title, source_name, content, token_estimate, embedding, created_at)
            VALUES
                (@id, @doc, @idx, @domain, @title, @source, @content, @tokens, @embedding, now());
            """;

        foreach (var chunk in chunks)
        {
            await using var cmd = new NpgsqlCommand(insert, conn, (NpgsqlTransaction)tx);
            cmd.Parameters.AddWithValue("id", chunk.Id);
            cmd.Parameters.AddWithValue("doc", chunk.DocumentId);
            cmd.Parameters.AddWithValue("idx", chunk.ChunkIndex);
            cmd.Parameters.AddWithValue("domain", chunk.CorpusId);
            cmd.Parameters.AddWithValue("title", chunk.Title);
            cmd.Parameters.AddWithValue("source", chunk.SourceName);
            cmd.Parameters.AddWithValue("content", chunk.Content);
            cmd.Parameters.AddWithValue("tokens", EstimateTokens(chunk.Content));
            cmd.Parameters.AddWithValue("embedding", new Vector(chunk.Embedding));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        logger.LogInformation("Replaced chunks doc={Doc} count={Count}", documentId, chunks.Count);
    }

    /// <inheritdoc />
    public async Task DeleteDocumentAsync(
        string corpusId,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var corpus = corpusId?.Trim() ?? string.Empty;
        if (corpus.Length == 0)
        {
            throw new KnowledgeCorpusRequiredException();
        }

        RequireConnectionString();
        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await using (var chunks = new NpgsqlCommand(
                         "DELETE FROM agent_knowledge_chunk WHERE document_id = @id AND domain = @domain",
                         conn,
                         (NpgsqlTransaction)tx))
        {
            chunks.Parameters.AddWithValue("id", documentId);
            chunks.Parameters.AddWithValue("domain", corpus);
            await chunks.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var docs = new NpgsqlCommand(
                         "DELETE FROM agent_knowledge_document WHERE id = @id AND domain = @domain",
                         conn,
                         (NpgsqlTransaction)tx))
        {
            docs.Parameters.AddWithValue("id", documentId);
            docs.Parameters.AddWithValue("domain", corpus);
            await docs.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        IReadOnlyList<string> corpusIds,
        double minSimilarity,
        CancellationToken cancellationToken = default)
    {
        var ids = CorpusIds.Normalize(corpusIds);
        if (ids.Count == 0)
        {
            throw new KnowledgeCorpusRequiredException();
        }

        RequireConnectionString();
        const string sql = """
            SELECT id, document_id, title, domain, source_name, content,
                   (1 - (embedding <=> @q)) AS score
            FROM agent_knowledge_chunk
            WHERE domain = ANY(@corpus)
              AND (1 - (embedding <=> @q)) >= @minScore
            ORDER BY embedding <=> @q
            LIMIT @k;
            """;

        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("q", new Vector(queryEmbedding));
        cmd.Parameters.Add(new NpgsqlParameter("corpus", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = ids.ToArray()
        });
        cmd.Parameters.Add(new NpgsqlParameter("minScore", NpgsqlDbType.Double) { Value = minSimilarity });
        cmd.Parameters.Add(new NpgsqlParameter("k", NpgsqlDbType.Integer) { Value = topK });

        var hits = new List<KnowledgeHit>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hits.Add(new KnowledgeHit
            {
                ChunkId = reader.GetString(0),
                DocumentId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Title = reader.GetString(2),
                CorpusId = reader.GetString(3),
                Source = reader.GetString(4),
                Snippet = Truncate(reader.GetString(5), 900),
                Score = Math.Round(Convert.ToDouble(reader.GetValue(6), CultureInfo.InvariantCulture), 3)
            });
        }

        return hits;
    }

    private void RequireConnectionString()
    {
        if (string.IsNullOrWhiteSpace(options.CurrentValue.ConnectionString))
        {
            throw new InvalidOperationException("未配置 Agent:Knowledge:ConnectionString。");
        }
    }

    private static int EstimateTokens(string content) => Math.Max(1, content.Length / 2);

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "...";
}

/// <summary>整理调用方传来的资料集编号。</summary>
public static class CorpusIds
{
    /// <summary>去掉空白项。结果为空表示调用方没有给出范围。</summary>
    public static IReadOnlyList<string> Normalize(IReadOnlyList<string>? corpusIds)
    {
        if (corpusIds is null || corpusIds.Count == 0)
        {
            return [];
        }

        return corpusIds
            .Select(id => id?.Trim() ?? string.Empty)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
