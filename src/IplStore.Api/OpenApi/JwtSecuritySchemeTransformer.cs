using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IplStore.Api.OpenApi;

/// <summary>
/// Adds the JWT bearer security scheme to the generated OpenAPI document so the
/// Scalar UI shows an "Authorize" affordance and sends the token on secured endpoints.
/// Targets Microsoft.OpenApi v2 (the version shipped with .NET 10).
/// </summary>
public sealed class JwtSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private readonly IAuthenticationSchemeProvider _schemeProvider;

    public JwtSecuritySchemeTransformer(IAuthenticationSchemeProvider schemeProvider)
        => _schemeProvider = schemeProvider;

    public async Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemes = await _schemeProvider.GetAllSchemesAsync();
        if (schemes.All(s => s.Name != "Bearer")) return;

        var bearerScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter the JWT access token (without the 'Bearer ' prefix)."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = bearerScheme;

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        };

        foreach (var operation in document.Paths.Values.SelectMany(p => p.Operations!.Values))
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(requirement);
        }
    }
}
