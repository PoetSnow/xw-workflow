namespace XW.Inspection.Domain;

/// <summary>法规目录和检测项目的持久化。</summary>
public interface IInspectionStore
{
    /// <summary>按 Id 读取法规，含行业和项目类型。</summary>
    Task<InspectionRegulation?> FindRegulationAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>按标准号读取法规。</summary>
    Task<InspectionRegulation?> FindRegulationByStandardNoAsync(string standardNo, CancellationToken cancellationToken = default);

    /// <summary>列出法规目录。</summary>
    Task<IReadOnlyList<InspectionRegulation>> ListRegulationsAsync(CancellationToken cancellationToken = default);

    /// <summary>已发布、且行业和项目类型都包含给定值的法规。</summary>
    Task<IReadOnlyList<InspectionRegulation>> ListMatchesAsync(string industry, string projectType, CancellationToken cancellationToken = default);

    /// <summary>加入一套新法规。</summary>
    void AddRegulation(InspectionRegulation regulation);

    /// <summary>按 Id 读取项目，含资料和检测项。</summary>
    Task<InspectionProject?> FindProjectAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>加入一个项目。</summary>
    void AddProject(InspectionProject project);

    /// <summary>去掉项目上的一条资料关联。</summary>
    void RemoveProjectRegulation(InspectionProjectRegulation link);

    /// <summary>加入一条检测项。</summary>
    void AddItem(InspectionProjectItem item);

    /// <summary>去掉一条检测项。</summary>
    void RemoveItem(InspectionProjectItem item);

    /// <summary>保存当前改动。</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
