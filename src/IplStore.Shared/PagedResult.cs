namespace IplStore.Shared;

public sealed record PaginationParams
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly int _pageSize = DefaultPageSize;

    public int Page { get; init; } = 1;

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public int Skip => (Math.Max(1, Page) - 1) * PageSize;
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int pageSize = PaginationParams.DefaultPageSize)
        => new(Array.Empty<T>(), 1, pageSize, 0);
}
