namespace IplStore.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "IplStore";
    public string Audience { get; set; } = "IplStore.Client";
    public string SecretKey { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
