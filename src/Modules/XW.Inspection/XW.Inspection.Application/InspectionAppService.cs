using System.Text.RegularExpressions;
using XW.Inspection.Application.Contracts;
using XW.Inspection.Domain;

namespace XW.Inspection.Application;

/// <summary>法规目录和检测项目草稿。</summary>
public sealed class InspectionAppService(IInspectionStore store, IAgentInspectionClient agent) : IInspectionAppService
{
    private static readonly Regex ClausePattern = new(@"第[0-9０-９一二三四五六七八九十百零〇千]+条", RegexOptions.Compiled);

    /// <inheritdoc />
    public async Task<RegulationDto> UploadAsync(UploadRegulationRequest request, CancellationToken cancellationToken = default)
    {
        var name = Required(request.Name, "名称不能为空。");
        var standardNo = Required(request.StandardNo, "标准号不能为空。");
        if (!request.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InspectionRuleException("只接受 PDF。");
        }

        if (await store.FindRegulationByStandardNoAsync(standardNo, cancellationToken) is not null)
        {
            throw new InspectionRuleException("这个标准号已经在目录里。新版本请换一个标准号后再上传。");
        }

        var ingested = await agent.IngestAsync(request.Content, request.FileName, standardNo, name, cancellationToken);
        if (!string.Equals(ingested.Status, "ready", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(ingested.DocumentId))
        {
            throw new InspectionRuleException(string.IsNullOrWhiteSpace(ingested.ErrorMessage) ? "法规入库失败。" : ingested.ErrorMessage);
        }

        var scope = await agent.SearchScopeAsync(standardNo, cancellationToken);
        var clauseNo = scope is null ? null : ReadClause(scope.Title, scope.Snippet);
        var regulation = new InspectionRegulation
        {
            Id = IdGenerater.GetNextId(),
            CorpusId = standardNo,
            Name = name,
            StandardNo = standardNo,
            Status = RegulationStatuses.Unpublished,
            DocumentId = ingested.DocumentId,
            ChunkId = scope?.ChunkId,
            ClauseNo = clauseNo,
            ScopeSnippet = Trim(scope?.Snippet, 2000)
        };
        FillIndustries(regulation, request.Industries);
        FillProjectTypes(regulation, request.ProjectTypes);
        store.AddRegulation(regulation);
        await store.SaveAsync(cancellationToken);
        return ToDto(regulation);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegulationDto>> ListRegulationsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await store.ListRegulationsAsync(cancellationToken);
        return rows.Select(ToDto).ToArray();
    }

    /// <inheritdoc />
    public async Task<RegulationDto> PublishAsync(long id, CancellationToken cancellationToken = default)
    {
        var regulation = await RequireRegulationAsync(id, cancellationToken);
        if (regulation.Status == RegulationStatuses.Withdrawn)
        {
            throw new InspectionRuleException("已废止的法规不能发布。");
        }

        if (regulation.Industries.Count == 0 || regulation.ProjectTypes.Count == 0)
        {
            throw new InspectionRuleException("行业和项目类型都要填写后才能发布。");
        }

        if (string.IsNullOrWhiteSpace(regulation.DocumentId))
        {
            throw new InspectionRuleException("法规还没有入库，不能发布。");
        }

        regulation.Status = RegulationStatuses.Published;
        await store.SaveAsync(cancellationToken);
        return ToDto(regulation);
    }

    /// <inheritdoc />
    public async Task<RegulationDto> WithdrawAsync(long id, CancellationToken cancellationToken = default)
    {
        var regulation = await RequireRegulationAsync(id, cancellationToken);
        regulation.Status = RegulationStatuses.Withdrawn;
        await store.SaveAsync(cancellationToken);
        return ToDto(regulation);
    }

    /// <inheritdoc />
    public async Task<RegulationDto> ReplaceProjectTypesAsync(long id, IReadOnlyList<string> projectTypes, CancellationToken cancellationToken = default)
    {
        var regulation = await RequireRegulationAsync(id, cancellationToken);
        EnsureEditable(regulation);
        regulation.ProjectTypes.Clear();
        FillProjectTypes(regulation, projectTypes);
        await store.SaveAsync(cancellationToken);
        return ToDto(regulation);
    }

    /// <inheritdoc />
    public async Task<RegulationDto> ReplaceIndustriesAsync(long id, IReadOnlyList<string> industries, CancellationToken cancellationToken = default)
    {
        var regulation = await RequireRegulationAsync(id, cancellationToken);
        EnsureEditable(regulation);
        regulation.Industries.Clear();
        FillIndustries(regulation, industries);
        await store.SaveAsync(cancellationToken);
        return ToDto(regulation);
    }

    /// <inheritdoc />
    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var project = new InspectionProject
        {
            Id = IdGenerater.GetNextId(),
            Industry = Required(request.Industry, "行业不能为空。"),
            ProjectType = Required(request.ProjectType, "项目类型不能为空。"),
            Medium = BlankToNull(request.Medium),
            BuildKind = BlankToNull(request.BuildKind),
            Status = "draft"
        };
        store.AddProject(project);
        await RematchAsync(project, cancellationToken);
        await store.SaveAsync(cancellationToken);
        return ToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto> GetProjectAsync(long id, CancellationToken cancellationToken = default)
    {
        var project = await RequireProjectAsync(id, cancellationToken);
        if (project.Status == "draft")
        {
            await RematchAsync(project, cancellationToken);
            await store.SaveAsync(cancellationToken);
        }

        return ToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto> RemoveRegulationAsync(long projectId, long regulationId, CancellationToken cancellationToken = default)
    {
        var project = await RequireDraftAsync(projectId, cancellationToken);
        var link = project.Regulations.FirstOrDefault(item => item.RegulationId == regulationId && !item.Removed)
                   ?? throw new InspectionNotFoundException("项目里没有这套资料。");
        link.Removed = true;
        await store.SaveAsync(cancellationToken);
        return ToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto> AddRegulationAsync(long projectId, long regulationId, CancellationToken cancellationToken = default)
    {
        var project = await RequireDraftAsync(projectId, cancellationToken);
        var regulation = await RequireRegulationAsync(regulationId, cancellationToken);
        if (regulation.Status != RegulationStatuses.Published)
        {
            throw new InspectionRuleException("只能补已发布的法规。");
        }

        var link = project.Regulations.FirstOrDefault(item => item.RegulationId == regulationId);
        if (link is null)
        {
            project.Regulations.Add(Link(project.Id, regulation, "manual"));
        }
        else if (link.Removed)
        {
            link.Removed = false;
            link.Source = "manual";
            link.Name = regulation.Name;
            link.CorpusId = regulation.CorpusId;
        }

        await store.SaveAsync(cancellationToken);
        return ToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto> ListItemsAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var project = await RequireDraftAsync(projectId, cancellationToken);
        var corpusIds = await KeptCorpusIdsAsync(project, cancellationToken);
        if (corpusIds.Count == 0)
        {
            throw new InspectionRuleException("没有留下的已发布资料，不能列检测项。");
        }

        var listed = await agent.ListItemsAsync(new AgentListItemsRequest
        {
            JobId = IdGenerater.GetNextId().ToString(),
            CorpusIds = corpusIds,
            Industry = project.Industry,
            ProjectType = project.ProjectType,
            Medium = project.Medium,
            BuildKind = project.BuildKind
        }, cancellationToken);

        foreach (var incoming in listed)
        {
            var name = incoming.Name.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var existing = project.Items.FirstOrDefault(item => item.Name == name);
            if (existing is null)
            {
                project.Items.Add(new InspectionProjectItem
                {
                    Id = IdGenerater.GetNextId(),
                    ProjectId = project.Id,
                    Name = Trim(name, 256) ?? name,
                    Requirement = Trim(incoming.Requirement, 2000) ?? string.Empty,
                    CorpusId = incoming.Basis.CorpusId,
                    DocumentId = incoming.Basis.DocumentId,
                    ChunkId = incoming.Basis.ChunkId,
                    ClauseNo = incoming.Basis.ClauseNo,
                    Snippet = Trim(incoming.Basis.Snippet, 2000) ?? string.Empty,
                    Source = "agent",
                    Edited = false
                });
                continue;
            }

            if (existing.Source == "manual" || existing.Edited)
            {
                continue;
            }

            existing.Requirement = Trim(incoming.Requirement, 2000) ?? string.Empty;
            existing.CorpusId = incoming.Basis.CorpusId;
            existing.DocumentId = incoming.Basis.DocumentId;
            existing.ChunkId = incoming.Basis.ChunkId;
            existing.ClauseNo = incoming.Basis.ClauseNo;
            existing.Snippet = Trim(incoming.Basis.Snippet, 2000) ?? string.Empty;
        }

        await store.SaveAsync(cancellationToken);
        return ToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto> AddItemAsync(long projectId, UpsertItemRequest request, CancellationToken cancellationToken = default)
    {
        var project = await RequireDraftAsync(projectId, cancellationToken);
        var name = ItemName(request.Name);
        if (project.Items.Any(item => item.Name == name))
        {
            throw new InspectionRuleException("已经有同名检测项。");
        }

        project.Items.Add(new InspectionProjectItem
        {
            Id = IdGenerater.GetNextId(),
            ProjectId = project.Id,
            Name = name,
            Requirement = ItemRequirement(request.Requirement),
            CorpusId = string.Empty,
            DocumentId = string.Empty,
            ChunkId = string.Empty,
            Snippet = string.Empty,
            Source = "manual",
            Edited = false
        });
        await store.SaveAsync(cancellationToken);
        return ToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto> UpdateItemAsync(long projectId, long itemId, UpsertItemRequest request, CancellationToken cancellationToken = default)
    {
        var project = await RequireDraftAsync(projectId, cancellationToken);
        var item = project.Items.FirstOrDefault(row => row.Id == itemId)
                   ?? throw new InspectionNotFoundException("没有这条检测项。");
        var name = ItemName(request.Name);
        if (project.Items.Any(row => row.Id != itemId && row.Name == name))
        {
            throw new InspectionRuleException("已经有同名检测项。");
        }

        item.Name = name;
        item.Requirement = ItemRequirement(request.Requirement);
        item.Edited = true;
        await store.SaveAsync(cancellationToken);
        return ToDto(project);
    }

    /// <inheritdoc />
    public async Task<ProjectDto> RemoveItemAsync(long projectId, long itemId, CancellationToken cancellationToken = default)
    {
        var project = await RequireDraftAsync(projectId, cancellationToken);
        var item = project.Items.FirstOrDefault(row => row.Id == itemId)
                   ?? throw new InspectionNotFoundException("没有这条检测项。");
        project.Items.Remove(item);
        store.RemoveItem(item);
        await store.SaveAsync(cancellationToken);
        return ToDto(project);
    }

    private async Task RematchAsync(InspectionProject project, CancellationToken cancellationToken)
    {
        var matches = await store.ListMatchesAsync(project.Industry, project.ProjectType, cancellationToken);
        var matchIds = matches.Select(item => item.Id).ToHashSet();
        foreach (var link in project.Regulations.Where(item => item.Source == "matched" && !item.Removed).ToList())
        {
            if (matchIds.Contains(link.RegulationId))
            {
                continue;
            }

            project.Regulations.Remove(link);
            store.RemoveProjectRegulation(link);
        }

        foreach (var regulation in matches)
        {
            if (project.Regulations.Any(item => item.RegulationId == regulation.Id))
            {
                continue;
            }

            project.Regulations.Add(Link(project.Id, regulation, "matched"));
        }
    }

    private async Task<IReadOnlyList<string>> KeptCorpusIdsAsync(InspectionProject project, CancellationToken cancellationToken)
    {
        var ids = new List<string>();
        foreach (var link in project.Regulations.Where(item => !item.Removed))
        {
            var regulation = await store.FindRegulationAsync(link.RegulationId, cancellationToken);
            if (regulation?.Status == RegulationStatuses.Published && !string.IsNullOrWhiteSpace(link.CorpusId))
            {
                ids.Add(link.CorpusId);
            }
        }

        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    private async Task<InspectionRegulation> RequireRegulationAsync(long id, CancellationToken cancellationToken) =>
        await store.FindRegulationAsync(id, cancellationToken) ?? throw new InspectionNotFoundException("没有这套法规。");

    private async Task<InspectionProject> RequireProjectAsync(long id, CancellationToken cancellationToken) =>
        await store.FindProjectAsync(id, cancellationToken) ?? throw new InspectionNotFoundException("没有这个项目。");

    private async Task<InspectionProject> RequireDraftAsync(long id, CancellationToken cancellationToken)
    {
        var project = await RequireProjectAsync(id, cancellationToken);
        if (project.Status != "draft")
        {
            throw new InspectionRuleException("只有草稿可以改。");
        }

        return project;
    }

    private static void EnsureEditable(InspectionRegulation regulation)
    {
        if (regulation.Status == RegulationStatuses.Withdrawn)
        {
            throw new InspectionRuleException("已废止的法规不能改适用行业或项目类型。");
        }
    }

    private static void FillIndustries(InspectionRegulation regulation, IReadOnlyList<string> industries)
    {
        foreach (var industry in Tags(industries))
        {
            regulation.Industries.Add(new InspectionRegulationIndustry
            {
                Id = IdGenerater.GetNextId(),
                RegulationId = regulation.Id,
                Industry = industry
            });
        }
    }

    private static void FillProjectTypes(InspectionRegulation regulation, IReadOnlyList<string> projectTypes)
    {
        foreach (var projectType in Tags(projectTypes))
        {
            regulation.ProjectTypes.Add(new InspectionRegulationProjectType
            {
                Id = IdGenerater.GetNextId(),
                RegulationId = regulation.Id,
                ProjectType = projectType
            });
        }
    }

    private static InspectionProjectRegulation Link(long projectId, InspectionRegulation regulation, string source) =>
        new()
        {
            Id = IdGenerater.GetNextId(),
            ProjectId = projectId,
            RegulationId = regulation.Id,
            CorpusId = regulation.CorpusId,
            Name = regulation.Name,
            Source = source,
            Removed = false
        };

    private static RegulationDto ToDto(InspectionRegulation regulation) =>
        new()
        {
            Id = regulation.Id,
            CorpusId = regulation.CorpusId,
            Name = regulation.Name,
            StandardNo = regulation.StandardNo,
            Status = regulation.Status,
            Industries = regulation.Industries.Select(item => item.Industry).ToArray(),
            ProjectTypes = regulation.ProjectTypes.Select(item => item.ProjectType).ToArray(),
            ClauseNo = regulation.ClauseNo,
            ScopeSnippet = regulation.ScopeSnippet
        };

    private static ProjectDto ToDto(InspectionProject project) =>
        new()
        {
            Id = project.Id,
            Industry = project.Industry,
            ProjectType = project.ProjectType,
            Medium = project.Medium,
            BuildKind = project.BuildKind,
            Status = project.Status,
            Regulations = project.Regulations.Where(item => !item.Removed).Select(item => new ProjectRegulationDto
            {
                RegulationId = item.RegulationId,
                CorpusId = item.CorpusId,
                Name = item.Name,
                Source = item.Source
            }).ToArray(),
            Items = project.Items.Select(item => new ProjectItemDto
            {
                Id = item.Id,
                Name = item.Name,
                Requirement = item.Requirement,
                CorpusId = item.CorpusId,
                DocumentId = item.DocumentId,
                ChunkId = item.ChunkId,
                ClauseNo = item.ClauseNo,
                Snippet = item.Snippet,
                Source = item.Source,
                Edited = item.Edited
            }).ToArray()
        };

    private static IEnumerable<string> Tags(IReadOnlyList<string>? values) =>
        (values ?? [])
        .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .Select(value => value.Length <= 64 ? value : value[..64]);

    private static string Required(string? value, string message)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            throw new InspectionRuleException(message);
        }

        return text;
    }

    private static string ItemName(string? value)
    {
        var name = Required(value, "检测项名称不能为空。");
        return name.Length <= 256 ? name : name[..256];
    }

    private static string ItemRequirement(string? value)
    {
        var text = Required(value, "法规要求不能为空。");
        return text.Length <= 2000 ? text : text[..2000];
    }

    private static string? BlankToNull(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length == 0 ? null : text;
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= max ? value : value[..max];
    }

    private static string? ReadClause(string title, string snippet)
    {
        var match = ClausePattern.Match($"{title}\n{snippet}");
        return match.Success ? match.Value : null;
    }
}

/// <summary>目录或草稿不存在。</summary>
public sealed class InspectionNotFoundException(string message) : Exception(message);

/// <summary>不满足上传、发布或匹配规则。</summary>
public sealed class InspectionRuleException(string message) : Exception(message);
