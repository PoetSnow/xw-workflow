namespace XW.Inspection.Application.Contracts;

/// <summary>上传并确认法规、按行业和项目类型匹配、列出检测项。</summary>
public interface IInspectionAppService : IAppService
{
    /// <summary>上传 PDF。行业或项目类型为空时保持未发布。</summary>
    Task<RegulationDto> UploadAsync(UploadRegulationRequest request, CancellationToken cancellationToken = default);

    /// <summary>法规目录。</summary>
    Task<IReadOnlyList<RegulationDto>> ListRegulationsAsync(CancellationToken cancellationToken = default);

    /// <summary>确认发布。行业和项目类型都要有值。</summary>
    Task<RegulationDto> PublishAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>废止。不重新解析 PDF。</summary>
    Task<RegulationDto> WithdrawAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>只改适用项目类型，不重新上传。</summary>
    Task<RegulationDto> ReplaceProjectTypesAsync(long id, IReadOnlyList<string> projectTypes, CancellationToken cancellationToken = default);

    /// <summary>只改适用行业，不重新上传。</summary>
    Task<RegulationDto> ReplaceIndustriesAsync(long id, IReadOnlyList<string> industries, CancellationToken cancellationToken = default);

    /// <summary>新建草稿并按目录匹配已发布资料。</summary>
    Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);

    /// <summary>打开草稿并重新匹配。已去掉的资料不会加回。</summary>
    Task<ProjectDto> GetProjectAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>从项目中去掉一套资料。</summary>
    Task<ProjectDto> RemoveRegulationAsync(long projectId, long regulationId, CancellationToken cancellationToken = default);

    /// <summary>从已发布目录补一套资料。</summary>
    Task<ProjectDto> AddRegulationAsync(long projectId, long regulationId, CancellationToken cancellationToken = default);

    /// <summary>只在留下的资料里列出检测项。不覆盖人工改过的行。</summary>
    Task<ProjectDto> ListItemsAsync(long projectId, CancellationToken cancellationToken = default);

    /// <summary>手工加一条检测项。</summary>
    Task<ProjectDto> AddItemAsync(long projectId, UpsertItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>修改检测项。改过的行再次分析时不覆盖。</summary>
    Task<ProjectDto> UpdateItemAsync(long projectId, long itemId, UpsertItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>删除一条检测项。</summary>
    Task<ProjectDto> RemoveItemAsync(long projectId, long itemId, CancellationToken cancellationToken = default);
}

/// <summary>上传法规。</summary>
public sealed class UploadRegulationRequest
{
    /// <summary>PDF 内容。</summary>
    public required Stream Content { get; init; }

    /// <summary>文件名。</summary>
    public required string FileName { get; init; }

    /// <summary>名称。</summary>
    public required string Name { get; init; }

    /// <summary>标准号。同时作为资料集编号。</summary>
    public required string StandardNo { get; init; }

    /// <summary>适用行业，可空。空则不能发布。</summary>
    public IReadOnlyList<string> Industries { get; init; } = [];

    /// <summary>适用项目类型，可空。空则不能发布。</summary>
    public IReadOnlyList<string> ProjectTypes { get; init; } = [];
}

/// <summary>手工填写或修改检测项。</summary>
public sealed class UpsertItemRequest
{
    /// <summary>名称。</summary>
    public required string Name { get; init; }

    /// <summary>法规要求。</summary>
    public required string Requirement { get; init; }
}

/// <summary>替换适用行业或项目类型。</summary>
public sealed class ReplaceTagsRequest
{
    /// <summary>标签列表。</summary>
    public IReadOnlyList<string> Values { get; init; } = [];
}

/// <summary>新建检测项目。</summary>
public sealed class CreateProjectRequest
{
    /// <summary>行业。</summary>
    public required string Industry { get; init; }

    /// <summary>项目类型。</summary>
    public required string ProjectType { get; init; }

    /// <summary>介质。</summary>
    public string? Medium { get; init; }

    /// <summary>新建、改建或扩建。</summary>
    public string? BuildKind { get; init; }
}

/// <summary>法规目录一行。</summary>
public sealed class RegulationDto
{
    /// <summary>目录 Id。</summary>
    public long Id { get; init; }

    /// <summary>资料集编号。</summary>
    public string CorpusId { get; init; } = string.Empty;

    /// <summary>名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>标准号。</summary>
    public string StandardNo { get; init; } = string.Empty;

    /// <summary>unpublished、published 或 withdrawn。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>适用行业。</summary>
    public IReadOnlyList<string> Industries { get; init; } = [];

    /// <summary>适用项目类型。</summary>
    public IReadOnlyList<string> ProjectTypes { get; init; } = [];

    /// <summary>适用范围条款号。</summary>
    public string? ClauseNo { get; init; }

    /// <summary>适用范围原文。</summary>
    public string? ScopeSnippet { get; init; }
}

/// <summary>检测项目。</summary>
public sealed class ProjectDto
{
    /// <summary>项目 Id。</summary>
    public long Id { get; init; }

    /// <summary>行业。</summary>
    public string Industry { get; init; } = string.Empty;

    /// <summary>项目类型。</summary>
    public string ProjectType { get; init; } = string.Empty;

    /// <summary>介质。</summary>
    public string? Medium { get; init; }

    /// <summary>建设性质。</summary>
    public string? BuildKind { get; init; }

    /// <summary>状态。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>留下的资料。去掉的不在这里。</summary>
    public IReadOnlyList<ProjectRegulationDto> Regulations { get; init; } = [];

    /// <summary>检测项。</summary>
    public IReadOnlyList<ProjectItemDto> Items { get; init; } = [];
}

/// <summary>项目上的一套资料。</summary>
public sealed class ProjectRegulationDto
{
    /// <summary>法规目录 Id。</summary>
    public long RegulationId { get; init; }

    /// <summary>资料集编号。</summary>
    public string CorpusId { get; init; } = string.Empty;

    /// <summary>名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>matched 或 manual。</summary>
    public string Source { get; init; } = string.Empty;
}

/// <summary>一条检测项。</summary>
public sealed class ProjectItemDto
{
    /// <summary>检测项 Id。</summary>
    public long Id { get; init; }

    /// <summary>名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>法规要求。</summary>
    public string Requirement { get; init; } = string.Empty;

    /// <summary>资料集编号。</summary>
    public string CorpusId { get; init; } = string.Empty;

    /// <summary>文档 Id。</summary>
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>切块 Id。</summary>
    public string ChunkId { get; init; } = string.Empty;

    /// <summary>条款号。</summary>
    public string? ClauseNo { get; init; }

    /// <summary>原文片段。</summary>
    public string Snippet { get; init; } = string.Empty;

    /// <summary>agent 或 manual。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>是否人工改过。</summary>
    public bool Edited { get; init; }
}
