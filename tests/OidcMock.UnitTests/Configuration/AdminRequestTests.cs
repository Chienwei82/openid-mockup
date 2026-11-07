using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;
using OidcMock.Core.Users;

namespace OidcMock.UnitTests.Configuration;

/// <summary>
/// Modelos de petición del endpoint de administración: clientes, usuarios y scopes.
/// </summary>
public sealed class AdminRequestTests
{
    [Fact]
    public void AdminClientRequestTieneTodosLosCampos()
    {
        var request = new AdminClientRequest
        {
            ClientId = "mi-cliente",
            ClientSecret = "secreto",
            RequireClientSecret = true,
            AllowedGrantTypes = ["client_credentials"],
            RedirectUris = ["https://app.example.com/callback"],
            TokenLifetimes = new TokenLifetimes
            {
                AccessToken = TimeSpan.FromMinutes(60),
                IdentityToken = TimeSpan.FromMinutes(10),
                RefreshToken = TimeSpan.FromDays(7)
            }
        };

        Assert.Equal("mi-cliente", request.ClientId);
        Assert.Equal("secreto", request.ClientSecret);
        Assert.True(request.RequireClientSecret);
        Assert.Equal(["client_credentials"], request.AllowedGrantTypes);
        Assert.Equal(["https://app.example.com/callback"], request.RedirectUris);
    }

    [Fact]
    public void AdminUserRequestTieneTodosLosCampos()
    {
        var request = new AdminUserRequest
        {
            Subject = "user-1",
            UserName = "jperez",
            Password = "clave",
            Claims = new Dictionary<string, string> { ["email"] = "jperez@example.com" }
        };

        Assert.Equal("user-1", request.Subject);
        Assert.Equal("jperez", request.UserName);
        Assert.Equal("clave", request.Password);
        Assert.Single(request.Claims);
    }

    [Fact]
    public void AdminScopeRequestTieneTodosLosCampos()
    {
        var request = new AdminScopeRequest
        {
            Name = "custom.scope",
            Claims = ["email", "full_name"]
        };

        Assert.Equal("custom.scope", request.Name);
        Assert.Equal(["email", "full_name"], request.Claims);
    }
}