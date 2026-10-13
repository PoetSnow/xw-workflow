namespace XW.System.Application.Contracts.Dtos.Notice;

/// <summary>
/// 通知公告分页查询参数
/// </summary>
public class NoticeSearchPagedDto : SearchPagedDto
{
    /// <summary>标题</summary>
    public string? Title { get; set; }

    /// <summary>发布状态</summary>
    public int? PublishStatus { get; set; }

    /// <summary>已读状态</summary>
    public int? IsRead { get; set; }
}
