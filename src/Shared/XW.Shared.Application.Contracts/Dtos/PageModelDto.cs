namespace XW.Shared.Application.Contracts.Dtos;

/// <summary>
/// 分页结果
/// </summary>
/// <typeparam name="T">数据项类型</typeparam>
[Serializable]
public class PageModelDto<T> : IDto
    where T : notnull
{
    private IReadOnlyList<T> _data = [];

    public PageModelDto()
    {
    }

    public PageModelDto(SearchPagedDto search)
        : this(search, [], default)
    {
    }

    public PageModelDto(SearchPagedDto search, IReadOnlyList<T> data, int count, dynamic? xData = null)
        : this(search.PageIndex, search.PageSize, data, count)
    {
        XData = xData ?? new object();
    }

    public PageModelDto(int pageIndex, int pageSize, IReadOnlyList<T> data, int count, dynamic? xData = null)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
        Total = count;
        List = data;
        XData = xData ?? new object();
    }

    /// <summary>当前页数据列表</summary>
    public IReadOnlyList<T> List
    {
        get => _data;
        set => _data = value ?? [];
    }

    /// <summary>当前页实际条数</summary>
    public int RowsCount => _data.Count;

    /// <summary>当前页码</summary>
    public int PageIndex { get; set; }

    /// <summary>每页条数</summary>
    public int PageSize { get; set; }

    /// <summary>总记录数</summary>
    public int Total { get; set; }

    /// <summary>总页数</summary>
    public int PageCount => (RowsCount + PageSize - 1) / PageSize;

    /// <summary>扩展数据</summary>
    public dynamic XData { get; set; } = new object();
}
