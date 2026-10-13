namespace XW.System.Application.Contracts.Dtos.Dict;

/// <summary>
/// 字典数据分页查询参数
/// </summary>
public class DictDataSearchPagedDto : SearchPagedDto
{
    /// <summary>字典编码</summary>
    public string? DictCode { get; set; }
}
