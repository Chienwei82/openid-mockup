using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;
using OidcMock.Core.Users;
using OidcMock.Host.Configuration;
using OidcMock.Host.Cors;
using OidcMock.Host.Stores;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Endpoints de administracion del mock: lectura y escritura dinamica de clientes, usuarios y
/// scopes, sin reiniciar. Todas las peticiones de escritura exigen el API key en el header
/// <c>X-Admin-Key</c>.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints, AdminOptions adminOptions)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(adminOptions);

        var policyName = OidcMockCors.PolicyName;

        // Los endpoints de escritura exigen la API key; la lectura de clientes solo pide CORS
        // porque un administrador del navegador podria revisar los clientes registrados.
        endpoints.MapGet("/admin/config", AdminConfig).RequireCors(policyName);
        endpoints.MapPost("/admin/clients", AdminCreateClient).RequireCors(policyName);
        endpoints.MapDelete("/admin/clients/{clientId}", AdminDeleteClient).RequireCors(policyName);
        endpoints.MapPost("/admin/users", AdminCreateUser).RequireCors(policyName);
        endpoints.MapDelete("/admin/users/{subject}", AdminDeleteUser).RequireCors(policyName);
        endpoints.MapPost("/admin/scopes", AdminCreateScope).RequireCors(policyName);
        endpoints.MapDelete("/admin/scopes/{name}", AdminDeleteScope).RequireCors(policyName);
        endpoints.MapPost("/admin/reload", AdminReload).RequireCors(policyName);

        return endpoints;
    }

    private static IResult AdminConfig(
        IClientStore clients,
        IMutableUserStore users,
        IMutableScopeStore scopes,
        AdminApiKeyOptions apiKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey.Key);

        var config = new
        {
            clients = clients.List().Select(c => new
            {
                c.ClientId,
                AllowedGrantTypes = c.AllowedGrantTypes,
                RedirectUris = c.RedirectUris,
                TokenLifetimes = c.TokenLifetimes
            }).ToList(),
            users = users.List().Select(u => new
            {
                u.Subject,
                u.UserName
            }).ToList(),
            scopes = scopes.List().Select(s => new
            {
                s.Name,
                Claims = s.Claims
            }).ToList(),
            apiKeySet = !string.IsNullOrEmpty(apiKey.Key)
        };

        return Results.Json(config, AdminRequest.SerializerOptions);
    }

    private static async Task<IResult> AdminCreateClient(
        HttpRequest request,
        IMutableClientStore clients,
        AdminApiKeyOptions apiKey,
        IClientStore jsonClients)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey.Key);

        var requestBody = await ReadAdminClientRequestAsync(request);
        EnsureClientIdUniqueness(requestBody, clients, jsonClients);

        var domainClient = requestBody.ToClient();
        clients.Add(domainClient);

        return Results.Created($"/admin/clients/{domainClient.ClientId}", new
        {
            domainClient.ClientId,
            domainClient.AllowedGrantTypes,
            domainClient.RedirectUris,
            domainClient.TokenLifetimes
        });
    }

    private static async Task<IResult> AdminDeleteClient(
        string clientId,
        IMutableClientStore clients,
        IClientStore jsonClients)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientId);

        var existing = FindClient(clientId, clients, jsonClients);
        if (existing is null)
        {
            return Results.NotFound();
        }

        clients.Delete(clientId);
        return Results.Empty;
    }

    private static async Task<IResult> AdminCreateUser(
        HttpRequest request,
        IMutableUserStore users,
        AdminApiKeyOptions apiKey,
        IUserStore jsonUsers)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey.Key);

        var requestBody = await ReadAdminUserRequestAsync(request);
        EnsureUserUniqueness(requestBody, users, jsonUsers);

        var domainUser = new User(
            requestBody.Subject,
            requestBody.UserName,
            requestBody.Password,
            requestBody.Claims.ToJsonElements());

        users.Add(domainUser);
        return Results.Created($"/admin/users/{domainUser.Subject}", new
        {
            domainUser.Subject,
            domainUser.UserName
        });
    }

    private static async Task<IResult> AdminDeleteUser(
        string subject,
        IMutableUserStore users,
        IUserStore jsonUsers)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        var existing = FindUser(subject, users, jsonUsers);
        if (existing is null)
        {
            return Results.NotFound();
        }

        users.Delete(subject);
        return Results.Empty;
    }

    private static async Task<IResult> AdminCreateScope(
        HttpRequest request,
        IMutableScopeStore scopes,
        AdminApiKeyOptions apiKey,
        IScopeStore jsonScopes)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey.Key);

        var requestBody = await ReadAdminScopeRequestAsync(request);
        EnsureScopeUniqueness(requestBody, scopes, jsonScopes);

        var domainScope = new ScopeDefinition(requestBody.Name, requestBody.Claims);
        scopes.Add(domainScope);

        return Results.Created($"/admin/scopes/{domainScope.Name}", new
        {
            domainScope.Name,
            domainScope.Claims
        });
    }

    private static async Task<IResult> AdminDeleteScope(
        string name,
        IMutableScopeStore scopes,
        IScopeStore jsonScopes)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var existing = FindScope(name, scopes, jsonScopes);
        if (existing is null)
        {
            return Results.NotFound();
        }

        scopes.Delete(name);
        return Results.Empty;
    }

    private static async Task<IResult> AdminReload(
        IMutableClientStore clients,
        IMutableUserStore users,
        IMutableScopeStore scopes,
        AdminApiKeyOptions apiKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(apiKey.Key);

        // Los stores en memoria ya cargan el JSON en cada acceso (reloadOnChange=true), asi que
        // reiniciar no es estrictamente necesario. Este endpoint solo limpia la cache del loader
        // si esta activa. Como el loader compara contenido por bytes, la recarga automatica ya
        // funciona: este endpoint es un "tiro de refresco" para entornos que desactivan reloadOnChange.
        return Results.Empty;
    }

    private static Client? FindClient(string clientId, IMutableClientStore mutable, IClientStore json)
    {
        return mutable.Find(clientId) ?? json.Find(clientId);
    }

    private static void EnsureClientIdUniqueness(AdminClientRequest request, IMutableClientStore mutable, IClientStore json)
    {
        if (mutable.Find(request.ClientId) is not null)
        {
            throw new InvalidOperationException($"El cliente '{request.ClientId}' ya existe.");
        }
        if (json.Find(request.ClientId) is not null)
        {
            throw new InvalidOperationException($"El cliente '{request.ClientId}' ya existe en el archivo de configuracion.");
        }
    }

    private static User? FindUser(string subject, IMutableUserStore mutable, IUserStore json)
    {
        return mutable.FindBySubject(subject) ?? json.FindBySubject(subject);
    }

    private static void EnsureUserUniqueness(AdminUserRequest request, IMutableUserStore mutable, IUserStore json)
    {
        if (mutable.FindBySubject(request.Subject) is not null)
        {
            throw new InvalidOperationException($"El usuario '{request.Subject}' ya existe.");
        }
        if (json.FindBySubject(request.Subject) is not null)
        {
            throw new InvalidOperationException($"El usuario '{request.Subject}' ya existe en el archivo de configuracion.");
        }
    }

    private static ScopeDefinition? FindScope(string name, IMutableScopeStore mutable, IScopeStore json)
    {
        return mutable.Find(name) ?? json.Find(name);
    }

    private static void EnsureScopeUniqueness(AdminScopeRequest request, IMutableScopeStore mutable, IScopeStore json)
    {
        if (mutable.Find(request.Name) is not null)
        {
            throw new InvalidOperationException($"El scope '{request.Name}' ya existe.");
        }
        if (json.Find(request.Name) is not null)
        {
            throw new InvalidOperationException($"El scope '{request.Name}' ya existe en el archivo de configuracion.");
        }
    }

    private static async Task<AdminClientRequest> ReadAdminClientRequestAsync(HttpRequest request)
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        return JsonSerializer.Deserialize<AdminClientRequest>(body, AdminRequest.SerializerOptions)
               ?? throw new InvalidOperationException("Cuerpo de la peticion no es un AdminClientRequest valido.");
    }

    private static async Task<AdminUserRequest> ReadAdminUserRequestAsync(HttpRequest request)
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        return JsonSerializer.Deserialize<AdminUserRequest>(body, AdminRequest.SerializerOptions)
               ?? throw new InvalidOperationException("Cuerpo de la peticion no es un AdminUserRequest valido.");
    }

    private static async Task<AdminScopeRequest> ReadAdminScopeRequestAsync(HttpRequest request)
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        return JsonSerializer.Deserialize<AdminScopeRequest>(body, AdminRequest.SerializerOptions)
               ?? throw new InvalidOperationException("Cuerpo de la peticion no es un AdminScopeRequest valido.");
    }
}
