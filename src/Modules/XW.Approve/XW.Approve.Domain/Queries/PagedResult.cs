namespace XW.Approve.Domain.Queries;

/// <summary>分页查询结果。</summary>
public sealed class PagedResult<T>
{
    /// <summary>当前页。</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>总条数。</summary>
    public int Total { get; init; }
}
