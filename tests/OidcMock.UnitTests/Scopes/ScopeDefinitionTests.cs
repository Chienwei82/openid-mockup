using OidcMock.Core.Scopes;

namespace OidcMock.UnitTests.Scopes;

public sealed class ScopeDefinitionTests
{
    [Fact]
    public void ExponeLosClaimsDeclarados()
    {
        var scope = new ScopeDefinition("custom.profile", ["full_name", "codTipoId"]);

        Assert.Equal("custom.profile", scope.Name);
        Assert.Contains("full_name", scope.Claims);
        Assert.Contains("codTipoId", scope.Claims);
    }

    [Fact]
    public void UnScopeSinClaimsEsValido()
    {
        var scope = new ScopeDefinition("offline_access", []);

        Assert.Empty(scope.Claims);
    }
}
