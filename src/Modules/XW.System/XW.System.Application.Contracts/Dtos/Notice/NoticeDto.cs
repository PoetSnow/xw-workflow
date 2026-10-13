namespace XW.System.Application.Contracts.Dtos.Notice;

/// <summary>
/// 通知公告详情
/// </summary>
[Serializable]
public class NoticeDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }

    /// <summary>标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>类型</summary>
    public int? Type { get; set; }

    /// <summary>发布人 Id</summary>
    public long? PublisherId { get; set; }

    /// <summary>优先级（0：低，1：中，2：高）</summary>
    public int? Priority { get; set; }

    /// <summary>目标类型（0：全部，1：指定用户）</summary>
    public int? TargetType { get; set; }

    /// <summary>发布状态（0：未发布，1：已发布，2：已撤回）</summary>
    public int? PublishStatus { get; set; }

    /// <summary>发布时间</summary>
    public DateTime? PublishTime { get; set; }

    /// <summary>撤回时间</summary>
    public DateTime? RevokeTime { get; set; }
}
