namespace XW.Agent.Contracts.Knowledge;

/// <summary>一条知识命中。只含引用所需的字段。</summary>
public sealed class KnowledgeHit
{
    /// <summary>资料集编号。对底座不透明。</summary>
    public required string CorpusId { get; init; }

    /// <summary>文档 Id。</summary>
    public required string DocumentId { get; init; }

    /// <summary>切块 Id。</summary>
    public required string ChunkId { get; init; }

    /// <summary>文档标题。</summary>
    public required string Title { get; init; }

    /// <summary>来源，例如文件名。</summary>
    public required string Source { get; init; }

    /// <summary>命中片段。</summary>
    public required string Snippet { get; init; }

    /// <summary>相似度分数。</summary>
    public double Score { get; init; }
}

/// <summary>检索请求。资料集编号由调用方给出。</summary>
public sealed class KnowledgeSearchRequest
{
    /// <summary>检索问句。</summary>
    public required string Query { get; init; }

    /// <summary>资料集编号列表。空列表会被拒绝，不会扫全库。</summary>
    public required IReadOnlyList<string> CorpusIds { get; init; }

    /// <summary>返回条数。小于等于 0 时由实现使用配置里的默认值。</summary>
    public int TopK { get; init; }
}

/// <summary>检索结果。拒绝和没有命中是两种状态。</summary>
public sealed class KnowledgeSearchResult
{
    /// <summary>为 true 时表示因缺少资料集而拒绝，没有执行检索。</summary>
    public bool Rejected { get; init; }

    /// <summary>拒绝原因。没有拒绝时为空。</summary>
    public string? Reason { get; init; }

    /// <summary>命中列表。拒绝或没有命中时为空。</summary>
    public IReadOnlyList<KnowledgeHit> Hits { get; init; } = [];

    /// <summary>缺少资料集，拒绝检索。</summary>
    public static KnowledgeSearchResult Reject(string reason) =>
        new() { Rejected = true, Reason = reason };

    /// <summary>检索已执行。命中可以为空。</summary>
    public static KnowledgeSearchResult Found(IReadOnlyList<KnowledgeHit> hits) =>
        new() { Hits = hits };
}

/// <summary>按资料集检索知识片段。</summary>
public interface IKnowledgeRetriever
{
    /// <summary>检索。资料集编号列表为空时拒绝，不扫全库。</summary>
    Task<KnowledgeSearchResult> SearchAsync(
        KnowledgeSearchRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>上传入库的请求。</summary>
public sealed class KnowledgeIngestRequest
{
    /// <summary>文件内容。</summary>
    public required Stream Content { get; init; }

    /// <summary>原始文件名。</summary>
    public required string FileName { get; init; }

    /// <summary>资料集编号。为空时拒绝，不会默认写成 general。</summary>
    public required string CorpusId { get; init; }

    /// <summary>MIME 类型，可空。</summary>
    public string? ContentType { get; init; }

    /// <summary>标题。空则取文件名。</summary>
    public string? Title { get; init; }

    /// <summary>指定文档 Id。空则自动生成。</summary>
    public string? DocumentId { get; init; }
}

/// <summary>入库结果。</summary>
public sealed class KnowledgeIngestResult
{
    /// <summary>文档 Id。</summary>
    public required string DocumentId { get; init; }

    /// <summary>标题。</summary>
    public required string Title { get; init; }

    /// <summary>资料集编号。</summary>
    public required string CorpusId { get; init; }

    /// <summary>ready 或 failed。</summary>
    public required string Status { get; init; }

    /// <summary>切块数。</summary>
    public int ChunkCount { get; init; }

    /// <summary>原件相对路径。</summary>
    public string? StoragePath { get; init; }

    /// <summary>失败原因。</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>知识入库与按资料集删除。</summary>
public interface IKnowledgeIngest
{
    /// <summary>解析、切块、向量化并写入指定资料集。缺少资料集编号时拒绝。</summary>
    Task<KnowledgeIngestResult> IngestAsync(
        KnowledgeIngestRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>删除指定资料集中的一篇文档及其切块。缺少资料集编号时拒绝。</summary>
    Task DeleteAsync(
        string corpusId,
        string documentId,
        CancellationToken cancellationToken = default);
}

/// <summary>调用方没有提供资料集编号。</summary>
public sealed class KnowledgeCorpusRequiredException : InvalidOperationException
{
    /// <summary>缺少资料集编号。</summary>
    public KnowledgeCorpusRequiredException()
        : base("缺少资料集编号。")
    {
    }
}

/// <summary>知识库连接，以及入库、检索要用的库内参数。绑定配置节 Agent:Knowledge。</summary>
public sealed class KnowledgeConnectionOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Agent:Knowledge";

    /// <summary>PostgreSQL 连接串。</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>总开关。关闭时检索返回空结果，不回退词法。</summary>
    public bool Enabled { get; set; }

    /// <summary>检索实现。仅 vector 会查库，其它值视为关闭。</summary>
    public string Provider { get; set; } = "vector";

    /// <summary>默认返回条数。</summary>
    public int TopK { get; set; } = 4;

    /// <summary>最低相似度，0 到 1。</summary>
    public double MinSimilarity { get; set; } = 0.25;

    /// <summary>切块目标字符数。</summary>
    public int ChunkSize { get; set; } = 700;

    /// <summary>切块重叠字符数。</summary>
    public int ChunkOverlap { get; set; } = 80;

    /// <summary>上传原件相对内容根的目录。</summary>
    public string UploadRelativePath { get; set; } = "knowledge/uploads";

    /// <summary>单文件上传上限，字节。</summary>
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>上传接口的服务端截止时间，秒。0 表示只跟随调用方取消。</summary>
    public int UploadDeadlineSeconds { get; set; } = 280;
}

/// <summary>OpenAI 兼容 Embedding。绑定配置节 Agent:Knowledge:Embedding。</summary>
public sealed class EmbeddingOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Agent:Knowledge:Embedding";

    /// <summary>关闭时不调用 Embedding，检索返回空结果。</summary>
    public bool Enabled { get; set; }

    /// <summary>服务根地址。</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>访问密钥，可空。</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>模型名。入库和查询必须一致。</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>向量维数。</summary>
    public int Dimensions { get; set; } = 1024;

    /// <summary>Embeddings 路径。</summary>
    public string EmbeddingsPath { get; set; } = "/v1/embeddings";

    /// <summary>HTTP 超时，秒。</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
