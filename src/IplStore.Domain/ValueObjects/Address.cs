using IplStore.Shared;

namespace IplStore.Domain.ValueObjects;

public sealed record Address(
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country)
{
    public static Result<Address> Create(
        string line1, string? line2, string city, string state, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(line1)) return Error.Validation("address.line1_required", "Line 1 is required.");
        if (string.IsNullOrWhiteSpace(city)) return Error.Validation("address.city_required", "City is required.");
        if (string.IsNullOrWhiteSpace(state)) return Error.Validation("address.state_required", "State is required.");
        if (string.IsNullOrWhiteSpace(postalCode)) return Error.Validation("address.postal_required", "Postal code is required.");
        if (string.IsNullOrWhiteSpace(country)) return Error.Validation("address.country_required", "Country is required.");

        return new Address(line1.Trim(), line2?.Trim(), city.Trim(), state.Trim(), postalCode.Trim(), country.Trim());
    }
}
