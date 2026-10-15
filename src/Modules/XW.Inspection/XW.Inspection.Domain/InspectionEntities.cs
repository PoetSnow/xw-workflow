using XW.Shared.Domain.Entities;

namespace XW.Inspection.Domain;

/// <summary>法规目录中的一套资料。</summary>
public class InspectionRegulation : DomainEntity
{
    /// <summary>资料集编号，与知识库 domain 相同。</summary>
    public string CorpusId { get; set; } = string.Empty;

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>标准号。</summary>
    public string StandardNo { get; set; } = string.Empty;

    /// <summary>unpublished、published、withdrawn。</summary>
    public string Status { get; set; } = RegulationStatuses.Unpublished;

    /// <summary>知识库文档 Id。</summary>
    public string? DocumentId { get; set; }

    /// <summary>适用范围所在切块。</summary>
    public string? ChunkId { get; set; }

    /// <summary>适用范围条款号。</summary>
    public string? ClauseNo { get; set; }

    /// <summary>适用范围原文。</summary>
    public string? ScopeSnippet { get; set; }

    /// <summary>适用行业。</summary>
    public List<InspectionRegulationIndustry> Industries { get; set; } = [];

    /// <summary>适用项目类型。</summary>
    public List<InspectionRegulationProjectType> ProjectTypes { get; set; } = [];
}

/// <summary>资料适用的一个行业。</summary>
public class InspectionRegulationIndustry : DomainEntity
{
    /// <summary>法规目录 Id。</summary>
    public long RegulationId { get; set; }

    /// <summary>行业。</summary>
    public string Industry { get; set; } = string.Empty;
}

/// <summary>资料适用的一种项目类型。</summary>
public class InspectionRegulationProjectType : DomainEntity
{
    /// <summary>法规目录 Id。</summary>
    public long RegulationId { get; set; }

    /// <summary>项目类型。</summary>
    public string ProjectType { get; set; } = string.Empty;
}

/// <summary>检测项目草稿。</summary>
public class InspectionProject : DomainEntity
{
    /// <summary>行业。</summary>
    public string Industry { get; set; } = string.Empty;

    /// <summary>项目类型。</summary>
    public string ProjectType { get; set; } = string.Empty;

    /// <summary>介质。</summary>
    public string? Medium { get; set; }

    /// <summary>新建、改建或扩建。</summary>
    public string? BuildKind { get; set; }

    /// <summary>draft。审批尚未接入。</summary>
    public string Status { get; set; } = "draft";

    /// <summary>匹配到或手工补上的资料。</summary>
    public List<InspectionProjectRegulation> Regulations { get; set; } = [];

    /// <summary>检测项。</summary>
    public List<InspectionProjectItem> Items { get; set; } = [];
}

/// <summary>项目留下的一套资料。</summary>
public class InspectionProjectRegulation : DomainEntity
{
    /// <summary>项目 Id。</summary>
    public long ProjectId { get; set; }

    /// <summary>法规目录 Id。</summary>
    public long RegulationId { get; set; }

    /// <summary>资料集编号。</summary>
    public string CorpusId { get; set; } = string.Empty;

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>matched 或 manual。</summary>
    public string Source { get; set; } = "matched";

    /// <summary>负责人去掉后为 true，再次匹配不会加回。</summary>
    public bool Removed { get; set; }
}

/// <summary>一条检测项。</summary>
public class InspectionProjectItem : DomainEntity
{
    /// <summary>项目 Id。</summary>
    public long ProjectId { get; set; }

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>法规要求。</summary>
    public string Requirement { get; set; } = string.Empty;

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

    /// <summary>agent 或 manual。</summary>
    public string Source { get; set; } = "agent";

    /// <summary>人工改过之后，再次分析不覆盖。</summary>
    public bool Edited { get; set; }
}

/// <summary>法规目录状态。</summary>
public static class RegulationStatuses
{
    /// <summary>已入库，尚未确认。</summary>
    public const string Unpublished = "unpublished";

    /// <summary>已确认，可被项目匹配。</summary>
    public const string Published = "published";

    /// <summary>废止。新草稿不再匹配。</summary>
    public const string Withdrawn = "withdrawn";
}
