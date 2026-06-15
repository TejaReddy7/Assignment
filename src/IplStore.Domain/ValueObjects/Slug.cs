using System.Text.RegularExpressions;
using IplStore.Shared;

namespace IplStore.Domain.ValueObjects;

public readonly partial record struct Slug(string Value)
{
    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNum();

    public static Result<Slug> Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Error.Validation("slug.empty", "Slug cannot be empty.");

        var normalized = NonAlphaNum()
            .Replace(raw.Trim().ToLowerInvariant(), "-")
            .Trim('-');

        if (normalized.Length == 0)
            return Error.Validation("slug.invalid", "Slug must contain alphanumeric characters.");
        if (normalized.Length > 200)
            normalized = normalized[..200].TrimEnd('-');

        return new Slug(normalized);
    }

    public override string ToString() => Value;
    public static implicit operator string(Slug s) => s.Value;
}
