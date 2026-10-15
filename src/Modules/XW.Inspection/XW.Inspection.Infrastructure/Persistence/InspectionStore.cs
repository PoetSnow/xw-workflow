using Microsoft.EntityFrameworkCore;
using XW.Inspection.Domain;

namespace XW.Inspection.Infrastructure.Persistence;

/// <summary>法规目录和项目草稿。</summary>
public sealed class InspectionStore(InspectionDbContext db) : IInspectionStore
{
    /// <inheritdoc />
    public Task<InspectionRegulation?> FindRegulationAsync(long id, CancellationToken cancellationToken = default) =>
        db.Regulations
            .Include(x => x.Industries)
            .Include(x => x.ProjectTypes)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<InspectionRegulation?> FindRegulationByStandardNoAsync(string standardNo, CancellationToken cancellationToken = default) =>
        db.Regulations
            .Include(x => x.Industries)
            .Include(x => x.ProjectTypes)
            .FirstOrDefaultAsync(x => x.StandardNo == standardNo, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<InspectionRegulation>> ListRegulationsAsync(CancellationToken cancellationToken = default) =>
        await db.Regulations
            .Include(x => x.Industries)
            .Include(x => x.ProjectTypes)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<InspectionRegulation>> ListMatchesAsync(
        string industry,
        string projectType,
        CancellationToken cancellationToken = default) =>
        await db.Regulations
            .Include(x => x.Industries)
            .Include(x => x.ProjectTypes)
            .Where(x => x.Status == RegulationStatuses.Published
                && x.Industries.Any(item => item.Industry == industry)
                && x.ProjectTypes.Any(item => item.ProjectType == projectType))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void AddRegulation(InspectionRegulation regulation) => db.Regulations.Add(regulation);

    /// <inheritdoc />
    public Task<InspectionProject?> FindProjectAsync(long id, CancellationToken cancellationToken = default) =>
        db.Projects
            .Include(x => x.Regulations)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public void AddProject(InspectionProject project) => db.Projects.Add(project);

    /// <inheritdoc />
    public void RemoveProjectRegulation(InspectionProjectRegulation link) => db.Remove(link);

    /// <inheritdoc />
    public void AddItem(InspectionProjectItem item) => db.Add(item);

    /// <inheritdoc />
    public void RemoveItem(InspectionProjectItem item) => db.Remove(item);

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
