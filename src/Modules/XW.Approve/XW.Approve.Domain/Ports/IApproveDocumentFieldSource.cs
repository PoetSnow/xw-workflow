namespace XW.Approve.Domain.Ports;

/// <summary>
/// 按 appr_business_field.FieldKey 读业务表列。FieldKey 就是列名，不映射实体属性。
/// </summary>
public interface IApproveDocumentFieldSource
{
    /// <summary>业务类型，如 Purchase。</summary>
    string BusinessType { get; }

    /// <summary>一次查出指定列。键必须与 FieldKey 一致。</summary>
    Task<IReadOnlyDictionary<string, object?>> ReadColumnsAsync(
        string businessId,
        IReadOnlyList<string> fieldKeys,
        CancellationToken cancellationToken = default);
}
