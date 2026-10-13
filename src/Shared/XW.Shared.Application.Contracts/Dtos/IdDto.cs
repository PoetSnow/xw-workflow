namespace XW.Shared.Application.Contracts.Dtos;

/// <summary>
/// 主键响应
/// </summary>
[Serializable]
public sealed class IdDto : OutputDto
{
    public IdDto()
    {
    }

    public IdDto(long id)
    {
        Id = id;
    }
}
