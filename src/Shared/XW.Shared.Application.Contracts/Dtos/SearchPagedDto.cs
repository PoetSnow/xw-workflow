namespace XW.Shared.Application.Contracts.Dtos;

/// <summary>
/// 分页查询基类
/// </summary>
public class SearchPagedDto : IDto
{
    private int _pageIndex;
    private int _pageSize;

    /// <summary>页码（从 1 开始）</summary>
    public int PageIndex
    {
        get => _pageIndex < 1 ? 1 : _pageIndex;
        set => _pageIndex = value;
    }

    /// <summary>每页条数（5–100）</summary>
    public int PageSize
    {
        get
        {
            if (_pageSize < 5)
            {
                _pageSize = 5;
            }

            if (_pageSize > 100)
            {
                _pageSize = 100;
            }

            return _pageSize;
        }
        set => _pageSize = value;
    }

    /// <summary>关键字</summary>
    public string? Keywords { get; set; }

    /// <summary>创建时间范围</summary>
    public DateTime[]? CreateTime { get; set; }
}
