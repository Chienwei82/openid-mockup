using System.Text.Json;
using OidcMock.Core.Configuration;
using OidcMock.Core.Clients;
using OidcMock.Core.Scopes;
using OidcMock.Core.Users;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Respuestas y serializadores del endpoint de administracion.
/// </summary>
public static class AdminResponse
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public sealed class AdminConfigResponse
    {
        public required IReadOnlyList<AdminClientResponse> Clients { get; init; } = [];
        public required IReadOnlyList<AdminUserResponse> Users { get; init; } = [];
        public required IReadOnlyList<AdminScopeResponse> Scopes { get; init; } = [];
        public required bool ApiKeySet { get; init; }
    }

    public sealed class AdminClientResponse
    {
        public required string ClientId { get; init; }
        public required IReadOnlyList<string> AllowedGrantTypes { get; init; }
        public required IReadOnlyList<string> RedirectUris { get; init; }
        public required TokenLifetimes TokenLifetimes { get; init; }

        public static AdminClientResponse From(AdminClientRequest request) => new()
        {
            ClientId = request.ClientId,
            AllowedGrantTypes = request.AllowedGrantTypes,
            RedirectUris = request.RedirectUris,
            TokenLifetimes = request.TokenLifetimes
        };
    }

    public sealed class AdminUserResponse
    {
        public required string Subject { get; init; }
        public required string UserName { get; init; }

        public static AdminUserResponse From(AdminUserRequest request) => new()
        {
            Subject = request.Subject,
            UserName = request.UserName
        };
    }

    public sealed class AdminScopeResponse
    {
        public required string Name { get; init; }
        public required IReadOnlyList<string> Claims { get; init; }

        public static AdminScopeResponse From(AdminScopeRequest request) => new()
        {
            Name = request.Name,
            Claims = request.Claims
        };
    }
}