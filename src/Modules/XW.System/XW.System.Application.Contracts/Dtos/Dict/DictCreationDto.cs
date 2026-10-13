namespace XW.System.Application.Contracts.Dtos.Dict;

/// <summary>
/// 字典创建参数
/// </summary>
public class DictCreationDto : InputDto
{
    /// <summary>编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>备注</summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>启用状态</summary>
    public bool Status { get; set; }
}

/// <summary>
/// 字典数据创建参数
/// </summary>
public class DictDataCreationDto : InputDto
{
    /// <summary>字典编码</summary>
    public string DictCode { get; set; } = string.Empty;

    /// <summary>显示标签</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>存储值</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>标签类型</summary>
    public string TagType { get; set; } = string.Empty;

    /// <summary>启用状态</summary>
    public bool Status { get; set; }

    /// <summary>排序号</summary>
    public int Ordinal { get; set; }
}
