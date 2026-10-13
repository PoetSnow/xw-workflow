using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using XW.Agent.Application.Options;
using Pgvector;

namespace XW.Agent.Application.Knowledge;

/// <summary>向量库中的文档元数据记录。</summary>
public sealed class KnowledgeDocumentRecord
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Domain { get; init; }
    public required string SourceName { get; init; }
    public required string ContentType { get; init; }
    public string? StoragePath { get; init; }
    public long ByteSize { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public int ChunkCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>写入向量库的切块（含 Embedding）。</summary>
public sealed class KnowledgeChunkWriteModel
{
    public required string Id { get; init; }
    public required string DocumentId { get; init; }
    public int ChunkIndex { get; init; }
    public required string Domain { get; init; }
    public required string Title { get; init; }
    public required string SourceName { get; init; }
    public required string Content { get; init; }
    public required float[] Embedding { get; init; }
}

/// <summary>pgvector 知识存储：文档元数据、切块向量与近邻检索。</summary>
public interface IKnowledgeVectorStore
{
    /// <summary>探测连接是否可用。</summary>
    Task EnsureReachableAsync(CancellationToken ct = default);

    /// <summary>插入或更新文档元数据。</summary>
    Task UpsertDocumentAsync(KnowledgeDocumentRecord doc, CancellationToken ct = default);

    /// <summary>替换某文档下全部切块向量。</summary>
    Task ReplaceChunksAsync(string documentId, IReadOnlyList<KnowledgeChunkWriteModel> chunks, CancellationToken ct = default);

    /// <summary>删除文档及其切块。</summary>
    Task DeleteDocumentAsync(string documentId, CancellationToken ct = default);

    /// <summary>按查询向量做近邻检索。</summary>
    Task<IReadOnlyList<KnowledgeHit>> SearchAsync(float[] queryEmbedding, int topK, string? domain, double minSimilarity, CancellationToken ct = default);

    /// <summary>文档/切块计数。</summary>
    Task<(int Documents, int Chunks)> CountAsync(CancellationToken ct = default);

    /// <summary>列出最近文档。</summary>
    Task<IReadOnlyList<KnowledgeDocumentRecord>> ListDocumentsAsync(int take = 50, CancellationToken ct = default);
}

/// <summary>PostgreSQL + pgvector 存储与近邻检索。</summary>
public sealed class PgVectorKnowledgeStore(
    KnowledgePgDataSource dataSource,
    IOptionsMonitor<AgentOptions> options,
    ILogger<PgVectorKnowledgeStore> logger) : IKnowledgeVectorStore
{
    /// <inheritdoc />
    public async Task EnsureReachableAsync(CancellationToken ct = default)
    {
        RequireConnectionString();
        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT 1", conn);
        _ = await cmd.ExecuteScalarAsync(ct);
    }

    public async Task UpsertDocumentAsync(KnowledgeDocumentRecord doc, CancellationToken ct = default)
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

        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", doc.Id);
        cmd.Parameters.AddWithValue("title", doc.Title);
        cmd.Parameters.AddWithValue("domain", doc.Domain);
        cmd.Parameters.AddWithValue("source", doc.SourceName);
        cmd.Parameters.AddWithValue("ctype", doc.ContentType);
        cmd.Parameters.AddWithValue("path", (object?)doc.StoragePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("size", doc.ByteSize);
        cmd.Parameters.AddWithValue("status", doc.Status);
        cmd.Parameters.AddWithValue("err", (object?)doc.ErrorMessage ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ReplaceChunksAsync(
        string documentId,
        IReadOnlyList<KnowledgeChunkWriteModel> chunks,
        CancellationToken ct = default)
    {
        RequireConnectionString();
        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var del = new NpgsqlCommand(
                         "DELETE FROM agent_knowledge_chunk WHERE document_id = @id", conn, (NpgsqlTransaction)tx))
        {
            del.Parameters.AddWithValue("id", documentId);
            await del.ExecuteNonQueryAsync(ct);
        }

        const string insert = """
            INSERT INTO agent_knowledge_chunk
                (id, document_id, chunk_index, domain, title, source_name, content, token_estimate, embedding, created_at)
            VALUES
                (@id, @doc, @idx, @domain, @title, @source, @content, @tokens, @embedding, now());
            """;

        foreach (var c in chunks)
        {
            await using var cmd = new NpgsqlCommand(insert, conn, (NpgsqlTransaction)tx);
            cmd.Parameters.AddWithValue("id", c.Id);
            cmd.Parameters.AddWithValue("doc", c.DocumentId);
            cmd.Parameters.AddWithValue("idx", c.ChunkIndex);
            cmd.Parameters.AddWithValue("domain", c.Domain);
            cmd.Parameters.AddWithValue("title", c.Title);
            cmd.Parameters.AddWithValue("source", c.SourceName);
            cmd.Parameters.AddWithValue("content", c.Content);
            cmd.Parameters.AddWithValue("tokens", EstimateTokens(c.Content));
            cmd.Parameters.AddWithValue("embedding", new Vector(c.Embedding));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        logger.LogInformation("Replaced chunks doc={Doc} count={Count}", documentId, chunks.Count);
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken ct = default)
    {
        RequireConnectionString();
        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM agent_knowledge_document WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", documentId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        string? domain,
        double minSimilarity,
        CancellationToken ct = default)
    {
        RequireConnectionString();
        const string sql = """
            SELECT id, title, domain, source_name, content,
                   (1 - (embedding <=> @q)) AS score
            FROM agent_knowledge_chunk
            WHERE (@domain IS NULL OR domain = @domain)
              AND (1 - (embedding <=> @q)) >= @minScore
            ORDER BY embedding <=> @q
            LIMIT @k;
            """;

        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("q", new Vector(queryEmbedding));
        // DBNull 无类型时 PG 报 42P08「无法确定参数 $n 的数据类型」
        cmd.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(domain) ? DBNull.Value : domain.Trim()
        });
        cmd.Parameters.Add(new NpgsqlParameter("minScore", NpgsqlDbType.Double) { Value = minSimilarity });
        cmd.Parameters.Add(new NpgsqlParameter("k", NpgsqlDbType.Integer) { Value = topK });

        var hits = new List<KnowledgeHit>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            hits.Add(new KnowledgeHit
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Domain = reader.GetString(2),
                Source = reader.GetString(3),
                // 与 ChunkSize≈700 对齐，避免保修分项等细则被截断后 LLM 只剩「12个月」
                Snippet = Truncate(reader.GetString(4), 900),
                Score = Math.Round(Convert.ToDouble(reader.GetValue(5), CultureInfo.InvariantCulture), 3)
            });
        }

        return hits;
    }

    public async Task<(int Documents, int Chunks)> CountAsync(CancellationToken ct = default)
    {
        RequireConnectionString();
        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT
              (SELECT COUNT(*) FROM agent_knowledge_document),
              (SELECT COUNT(*) FROM agent_knowledge_chunk);
            """, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return (0, 0);
        }

        return (Convert.ToInt32(reader.GetValue(0)), Convert.ToInt32(reader.GetValue(1)));
    }

    public async Task<IReadOnlyList<KnowledgeDocumentRecord>> ListDocumentsAsync(
        int take = 50,
        CancellationToken ct = default)
    {
        RequireConnectionString();
        const string sql = """
            SELECT d.id, d.title, d.domain, d.source_name, d.content_type, d.storage_path,
                   d.byte_size, d.status, d.error_message, d.created_at,
                   (SELECT COUNT(*) FROM agent_knowledge_chunk c WHERE c.document_id = d.id) AS chunk_count
            FROM agent_knowledge_document d
            ORDER BY d.created_at DESC
            LIMIT @take;
            """;

        await using var conn = await dataSource.Inner.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("take", take);

        var list = new List<KnowledgeDocumentRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new KnowledgeDocumentRecord
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Domain = reader.GetString(2),
                SourceName = reader.GetString(3),
                ContentType = reader.GetString(4),
                StoragePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                ByteSize = reader.GetInt64(6),
                Status = reader.GetString(7),
                ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(9),
                ChunkCount = Convert.ToInt32(reader.GetValue(10))
            });
        }

        return list;
    }

    private void RequireConnectionString()
    {
        if (string.IsNullOrWhiteSpace(options.CurrentValue.Knowledge.ConnectionString))
        {
            throw new InvalidOperationException(
                "未配置 Agent:Knowledge:ConnectionString。请安装 PostgreSQL+pgvector 后填入连接串，并执行 docs/sql/xw_agent.postgresql.sql。");
        }
    }

    private static int EstimateTokens(string content) => Math.Max(1, content.Length / 2);

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Length <= max ? s : s[..max] + "...";
}
