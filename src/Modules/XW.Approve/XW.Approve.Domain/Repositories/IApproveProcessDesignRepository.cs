using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Models;

namespace XW.Approve.Domain.Repositories;

/// <summary>流程设计器落库。只改配置表，不碰运行中的实例。</summary>
public interface IApproveProcessDesignRepository
{
    /// <summary>全部流程版本，新修改的在前。</summary>
    Task<IReadOnlyList<ApproveProcessDef>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>按主键取定义，不存在返回 null。</summary>
    Task<ApproveProcessDef?> FindDefAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>同编码是否已有版本。</summary>
    Task<bool> KeyExistsAsync(string processKey, CancellationToken cancellationToken = default);

    /// <summary>同编码下一个版本号。</summary>
    Task<int> NextVersionAsync(string processKey, CancellationToken cancellationToken = default);

    /// <summary>读取设计图。没有节点时仍返回定义。</summary>
    Task<ApproveProcessDesignRecord?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>新建草稿及整张图。</summary>
    Task InsertAsync(ApproveProcessDesignRecord record, CancellationToken cancellationToken = default);

    /// <summary>覆盖草稿图。调用方须已确认状态是 Draft。</summary>
    Task ReplaceDraftAsync(ApproveProcessDesignRecord record, CancellationToken cancellationToken = default);

    /// <summary>发布当前版本，停用同编码的其它 Active，并写成该业务的默认绑定。</summary>
    Task PublishAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>删除草稿及其图、绑定。</summary>
    Task DeleteDraftAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>某业务类型的条件字段。</summary>
    Task<IReadOnlyList<ApproveBusinessField>> ListFieldsAsync(string businessType, CancellationToken cancellationToken = default);

    /// <summary>按业务类型整表替换条件字段。</summary>
    Task ReplaceFieldsAsync(string businessType, IReadOnlyList<ApproveBusinessField> fields, CancellationToken cancellationToken = default);
}
