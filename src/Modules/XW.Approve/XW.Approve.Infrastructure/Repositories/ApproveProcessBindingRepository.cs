using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Repositories;
using XW.Approve.Infrastructure.Persistence;

namespace XW.Approve.Infrastructure.Repositories;

internal sealed class ApproveProcessBindingRepository(ApproveDbContext db) : IApproveProcessBindingRepository
{
    /// <inheritdoc />
    public async Task<ApproveProcessBinding?> FindForBusinessAsync(
        string businessType,
        string? businessSubType = null,
        CancellationToken cancellationToken = default)
    {
        var type = businessType.Trim();
        var sub = (businessSubType ?? string.Empty).Trim();

        var enabled = await db.Set<ApproveProcessBinding>()
            .AsNoTracking()
            .Where(x => x.Enabled && x.BusinessType == type)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (enabled.Count == 0)
        {
            return null;
        }

        IEnumerable<ApproveProcessBinding> scoped = enabled;
        if (!string.IsNullOrEmpty(sub))
        {
            var matchedSub = enabled.Where(x => x.BusinessSubType == sub).ToList();
            if (matchedSub.Count > 0)
            {
                scoped = matchedSub;
            }
        }

        return scoped.FirstOrDefault(x => x.IsDefault) ?? scoped.FirstOrDefault();
    }
}
