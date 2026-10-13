using XW.Approve.Domain.Entities;

namespace XW.Approve.Domain.Repositories;

/// <summary>按业务类型选流程绑定。P0 走默认绑定。</summary>
public interface IApproveProcessBindingRepository
{
    /// <summary>取该业务当前启用的默认绑定；没有默认则取优先级最高的启用项。</summary>
    Task<ApproveProcessBinding?> FindForBusinessAsync(
        string businessType,
        string? businessSubType = null,
        CancellationToken cancellationToken = default);
}
