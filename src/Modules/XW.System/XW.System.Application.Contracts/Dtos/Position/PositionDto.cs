namespace XW.System.Application.Contracts.Dtos.Position;

/// <summary>岗位详情</summary>
[Serializable]
public class PositionDto : PositionCreationDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }
}
