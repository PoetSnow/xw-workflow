namespace XW.System.Application.Contracts.Dtos.Menu;

/// <summary>
/// 菜单详情
/// </summary>
[Serializable]
public class MenuDto : MenuCreationDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }
}
