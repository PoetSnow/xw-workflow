namespace XW.System.Application.Contracts.Dtos.Dict;

/// <summary>
/// 字典选项集合
/// </summary>
[Serializable]
public class DictOptionDto
{
    /// <summary>名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>字典数据选项列表</summary>
    public DictDataOption[] DictDataList { get; set; } = [];

    /// <summary>
    /// 字典数据选项
    /// </summary>
    [Serializable]
    public class DictDataOption
    {
        /// <summary>显示标签</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>存储值</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>标签类型</summary>
        public string TagType { get; set; } = string.Empty;
    }
}
