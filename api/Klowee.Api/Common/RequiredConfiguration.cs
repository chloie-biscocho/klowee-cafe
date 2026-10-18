namespace Klowee.Api.Common;

/// <summary>
/// The settings the API cannot run without, checked together at startup so a
/// misconfigured deploy fails once with the whole list instead of one key per
/// restart. Names are given in environment-variable form (<c>Section__Key</c>),
/// which is how they are set on the host.
/// </summary>
public static class RequiredConfiguration
{
    private static readonly string[] Always =
    [
        "ConnectionStrings:Default",
        "Jwt:Key",
        "Supabase:Url",
        "Supabase:ServiceRoleKey"
    ];

    private static readonly string[] ProductionOnly =
    [
        "App:AllowedOrigins"
    ];

    /// <summary>Every required key that is missing or blank, as <c>Section__Key</c>.</summary>
    public static IReadOnlyList<string> Missing(IConfiguration configuration, IHostEnvironment environment)
    {
        var required = environment.IsProduction() ? Always.Concat(ProductionOnly) : Always;

        return required
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .Select(key => key.Replace(":", "__"))
            .ToList();
    }
}
