using OidcMock.Core.Configuration;

namespace OidcMock.UnitTests.Configuration;

/// <summary>
/// Politica para decidir el <c>sub</c> por defecto cuando no hay un usuario autenticado.
/// </summary>
public sealed class SubjectPolicyTests
{
    [Fact]
    public void LosTresValoresExisten()
    {
        Assert.Equal("User", SubjectPolicy.User.ToString());
        Assert.Equal("ClientId", SubjectPolicy.ClientId.ToString());
        Assert.Equal("IpAddress", SubjectPolicy.IpAddress.ToString());
    }

    [Fact]
    public void ElValorPorDefectoEsUser()
    {
        Assert.Equal(SubjectPolicy.User, SubjectPolicy.User);
    }
}