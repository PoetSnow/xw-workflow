namespace XW.Inspection.Domain;

/// <summary>调用 Agent 宿主的入库、检索和列检测项作业。</summary>
public interface IAgentInspectionClient
{
    /// <summary>把 PDF 写入指定资料集。</summary>
    Task<AgentIngestedDocument> IngestAsync(Stream content, string fileName, string corpusId, string title, CancellationToken cancellationToken = default);

    /// <summary>在一个资料集里检索适用范围。</summary>
    Task<AgentHit?> SearchScopeAsync(string corpusId, CancellationToken cancellationToken = default);

    /// <summary>只在给定资料集里列出检测项。</summary>
    Task<IReadOnlyList<AgentListedItem>> ListItemsAsync(AgentListItemsRequest request, CancellationToken cancellationToken = default);
}

/// <summary>入库结果。</summary>
public sealed class AgentIngestedDocument
{
    /// <summary>文档 Id。</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>ready 或 failed。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>失败原因。</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>一条检索命中。</summary>
public sealed class AgentHit
{
    /// <summary>切块 Id。</summary>
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>片段。</summary>
    public string Snippet { get; set; } = string.Empty;
}

/// <summary>列检测项的请求。</summary>
public sealed class AgentListItemsRequest
{
    /// <summary>作业 Id。</summary>
    public required string JobId { get; init; }

    /// <summary>留下的资料集编号。</summary>
    public required IReadOnlyList<string> CorpusIds { get; init; }

    /// <summary>行业。</summary>
    public required string Industry { get; init; }

    /// <summary>项目类型。</summary>
    public required string ProjectType { get; init; }

    /// <summary>介质。</summary>
    public string? Medium { get; init; }

    /// <summary>建设性质。</summary>
    public string? BuildKind { get; init; }
}

/// <summary>作业返回的一条检测项。</summary>
public sealed class AgentListedItem
{
    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>法规要求。</summary>
    public string Requirement { get; set; } = string.Empty;

    /// <summary>依据。</summary>
    public AgentItemBasis Basis { get; set; } = new();
}

/// <summary>检测项依据。</summary>
public sealed class AgentItemBasis
{
    /// <summary>资料集编号。</summary>
    public string CorpusId { get; set; } = string.Empty;

    /// <summary>文档 Id。</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>切块 Id。</summary>
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>条款号。</summary>
    public string? ClauseNo { get; set; }

    /// <summary>原文片段。</summary>
    public string Snippet { get; set; } = string.Empty;
}
