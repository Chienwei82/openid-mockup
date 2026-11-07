using OidcMock.Core.Clients;
using OidcMock.Core.Tokens;

namespace OidcMock.Core.Configuration;

/// <summary>
/// Petición de creación o actualización de un cliente a través del endpoint de administración.
/// </summary>
public sealed class AdminClientRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public bool RequireClientSecret { get; set; }
    public IReadOnlyList<string> AllowedGrantTypes { get; set; } = [];
    public IReadOnlyList<string> RedirectUris { get; set; } = [];
    public TokenLifetimes TokenLifetimes { get; set; } = new(
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromDays(7),
        TimeSpan.FromMinutes(5));
}