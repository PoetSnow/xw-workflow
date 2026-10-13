using XW.Approve.Domain.Aggregates;

namespace XW.Approve.Domain.Ports;

/// <summary>待办附件落盘。键由实现生成，调用方只存返回值。</summary>
public interface IApproveTaskFileStore
{
    /// <summary>写入文件，返回存储键。</summary>
    Task<string> SaveAsync(long taskId, long fileId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>按存储键打开只读流。不存在则抛。</summary>
    Stream OpenRead(string storageKey);

    /// <summary>删除已写入的文件。不存在则忽略。</summary>
    void Delete(string storageKey);
}
