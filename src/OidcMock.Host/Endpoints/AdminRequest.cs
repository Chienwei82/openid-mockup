using System.Collections.Generic;
using System.Text.Json;
using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Tipos de peticion y serializadores del endpoint de administracion.
/// </summary>
public static class AdminRequest
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public sealed class AdminClientRequest
    {
        public required string ClientId { get; init; }
        public string? ClientSecret { get; set; }
        public bool RequireClientSecret { get; set; }
        public required IReadOnlyList<string> AllowedGrantTypes { get; init; } = [];
        public required IReadOnlyList<string> RedirectUris { get; init; } = [];
        public TokenLifetimes TokenLifetimes { get; init; } = new(
            TimeSpan.FromMinutes(60),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromDays(7),
            TimeSpan.FromMinutes(5));
    }

    public sealed class AdminUserRequest
    {
        public required string Subject { get; init; }
        public required string UserName { get; init; }
        public required string Password { get; init; }
        public required IReadOnlyDictionary<string, string> Claims { get; init; } = new Dictionary<string, string>();
    }

    public sealed class AdminScopeRequest
    {
        public required string Name { get; init; }
        public required IReadOnlyList<string> Claims { get; init; } = [];
    }
}