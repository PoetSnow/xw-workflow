namespace XW.System.Application.Contracts.Dtos.Dict;

/// <summary>
/// 字典详情
/// </summary>
[Serializable]
public class DictDto : DictCreationDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }
}

/// <summary>
/// 字典数据详情
/// </summary>
[Serializable]
public class DictDataDto : DictDataCreationDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }
}
