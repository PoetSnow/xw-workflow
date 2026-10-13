namespace XW.Shared.Application.Contracts.Dtos;

public static class SearchPagedDtoExtension
{
    /// <summary>
    /// 计算分页查询应跳过的行数（用于 Skip/Take）。
    /// </summary>
    public static int SkipRows(this SearchPagedDto dto) => (dto.PageIndex - 1) * dto.PageSize;
}
