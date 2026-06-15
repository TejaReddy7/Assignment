namespace IplStore.Application.Common;

/// <summary>Centralized cache key builders so invalidation prefixes stay consistent.</summary>
public static class CacheKeys
{
    public const string ProductPrefix = "products:";
    public const string FranchisePrefix = "franchises:";

    public static string ProductList(int page, int pageSize, string? sortBy, string? sortDir)
        => $"{ProductPrefix}list:p{page}:s{pageSize}:{sortBy}:{sortDir}";

    public static string ProductDetails(string slug) => $"{ProductPrefix}details:{slug}";

    public static string FranchiseList() => $"{FranchisePrefix}list";
}
