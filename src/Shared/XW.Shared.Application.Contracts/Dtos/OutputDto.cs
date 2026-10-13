namespace XW.Shared.Application.Contracts.Dtos;

/// <summary>
/// 输出 DTO 基类
/// </summary>
[Serializable]
public abstract class OutputDto : IDto
{
    /// <summary>主键</summary>
    public virtual long Id { get; set; }
}
