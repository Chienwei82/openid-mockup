using System.Text.Json;
using OidcMock.Core.Claims;
using OidcMock.Core.Scopes;
using OidcMock.UnitTests.Tokens;
using OidcMock.Core.Users;

namespace OidcMock.UnitTests.Claims;

public sealed class ScopesClaimsProjectorTests
{
    private readonly ScopesClaimsProjector _projector = new(new InMemoryScopeStore(
    [
        new ScopeDefinition("openid", ["sub"]),
        new ScopeDefinition("email", ["email", "email_verified"]),
        new ScopeDefinition("custom.profile", ["full_name", "nombre"]),
        new ScopeDefinition("KYC", ["documentofva"]),
        new ScopeDefinition("roles", ["role"]),
        new ScopeDefinition("offline_access", [])
    ]));

    [Fact]
    public void TomaLosClaimsDeclaradosPorLosScopesSolicitados()
    {
        var projected = _projector.Project(SampleUser(), ["openid", "email"]);

        Assert.Equal(["sub", "email", "email_verified"], projected.Keys);
    }

    [Fact]
    public void ConsolidaLosClaimsDeTodosLosScopesSolicitados()
    {
        var projected = _projector.Project(SampleUser(), ["openid", "custom.profile", "roles"]);

        Assert.Equal(["sub", "full_name", "nombre", "role"], projected.Keys);
    }

    [Fact]
    public void UnScopeSinClaimsNoProyectaNingunClaim()
    {
        var projected = _projector.Project(SampleUser(), ["openid", "offline_access"]);

        Assert.Equal(["sub"], projected.Keys);
    }

    [Fact]
    public void SinScopesNoProyectaNingunClaim()
    {
        var projected = _projector.Project(SampleUser(), []);

        Assert.Empty(projected);
    }

    [Fact]
    public void UnScopeDesconocidoNoProyectaNingunClaim()
    {
        var projected = _projector.Project(SampleUser(), ["scope.inventado"]);

        Assert.Empty(projected);
    }

    [Fact]
    public void ElClaimSubSeResuelveConElSubjectDelUsuario()
    {
        var projected = _projector.Project(SampleUser(), ["openid"]);

        Assert.Equal("user-1", projected["sub"].GetString());
    }

    [Fact]
    public void OmiteLosClaimsDeclaradosQueElUsuarioNoTiene()
    {
        var projected = _projector.Project(SampleUser(), ["openid", "KYC"]);

        Assert.Equal(["sub"], projected.Keys);
    }

    [Fact]
    public void ConservaElTipoJsonDelValorProyectado()
    {
        var projected = _projector.Project(SampleUser(), ["openid", "email", "roles"]);

        Assert.True(projected["email_verified"].GetBoolean());
        Assert.Equal(["administrador"], projected["role"].EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void NoRepiteUnClaimDeclaradoPorVariosScopes()
    {
        var projected = _projector.Project(SampleUser(), ["custom.profile", "nombre"]);

        Assert.Single(projected.Keys, key => string.Equals(key, "nombre", StringComparison.Ordinal));
    }

    private static User SampleUser() => new(
        "user-1",
        "jperez",
        "clave",
        new Dictionary<string, JsonElement>
        {
            ["email"] = JsonSerializer.SerializeToElement("jperez@example.cr"),
            ["email_verified"] = JsonSerializer.SerializeToElement(true),
            ["full_name"] = JsonSerializer.SerializeToElement("JUAN PEREZ LOPEZ"),
            ["nombre"] = JsonSerializer.SerializeToElement("Juan"),
            ["role"] = JsonSerializer.SerializeToElement<string[]>(["administrador"]),
            ["no_declarado"] = JsonSerializer.SerializeToElement("no debe salir")
        });
}
