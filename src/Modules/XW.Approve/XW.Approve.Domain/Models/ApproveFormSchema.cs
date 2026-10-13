namespace XW.Approve.Domain.Models;

/// <summary>一环的交付清单。和条件字段无关。</summary>
public sealed class ApproveFormSchema
{
    /// <summary>要填的字段。</summary>
    public List<ApproveFormField> Fields { get; set; } = [];

    /// <summary>是否允许附件。</summary>
    public bool AllowAttachment { get; set; }

    /// <summary>是否必须至少一条附件。</summary>
    public bool AttachmentRequired { get; set; }
}

/// <summary>清单里的一个字段。</summary>
public sealed class ApproveFormField
{
    /// <summary>同一份清单内唯一。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>界面名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>String / Text / Number / Date / Enum。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>是否必填。</summary>
    public bool Required { get; set; }

    /// <summary>枚举选项。</summary>
    public List<string> Options { get; set; } = [];
}
