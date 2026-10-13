using XW.Shared.Domain.Entities;

namespace XW.Approve.Domain.Entities;

/// <summary>协作或任务待办附件。对应 appr_task_file。文件本体不进本表。</summary>
public class ApproveTaskFile : DomainEntity
{
    /// <summary>文件名最大长度。</summary>
    public static readonly int FileNameMaxLength = 256;

    /// <summary>内容类型最大长度。</summary>
    public static readonly int ContentTypeMaxLength = 128;

    /// <summary>存储键最大长度。</summary>
    public static readonly int StorageKeyMaxLength = 512;

    /// <summary>所属待办 Id。</summary>
    public long TaskId { get; set; }

    /// <summary>所属实例 Id。</summary>
    public long InstanceId { get; set; }

    /// <summary>原始文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>内容类型。</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>字节数。</summary>
    public long FileSize { get; set; }

    /// <summary>本地或对象存储键。</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>上传人。</summary>
    public long CreateBy { get; set; }

    /// <summary>上传时间。</summary>
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
