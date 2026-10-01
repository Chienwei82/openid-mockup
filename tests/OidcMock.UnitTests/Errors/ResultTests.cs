using OidcMock.Core.Errors;

namespace OidcMock.UnitTests.Errors;

public sealed class ResultTests
{
    [Fact]
    public void UnExitoExponeElValorYNoElError()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.Succeeded);
        Assert.False(result.Failed);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void UnFalloExponeElErrorYNoElValor()
    {
        var result = Result<string>.Fail(ProtocolErrors.InvalidGrant("codigo caducado"));

        Assert.True(result.Failed);
        Assert.Equal("invalid_grant", result.Error?.Code);
        Assert.Null(result.Value);
    }

    [Fact]
    public void MapTransformaElValorYPropagaElError()
    {
        var mapped = Result<int>.Ok(21).Map(value => value * 2);
        var failed = Result<int>.Fail(ProtocolErrors.InvalidScope("scope desconocido")).Map(value => value * 2);

        Assert.Equal(42, mapped.Value);
        Assert.Equal("invalid_scope", failed.Error?.Code);
    }

    [Fact]
    public void ValueOrThrowDevuelveElValorDelExito()
    {
        var result = Result<string>.Ok("ok");

        Assert.Equal("ok", result.ValueOrThrow());
    }

    [Fact]
    public void ValueOrThrowLanzaCuandoLaOperacionFallo()
    {
        var result = Result<string>.Fail(ProtocolErrors.InvalidClient("secreto incorrecto"));

        var exception = Assert.Throws<InvalidOperationException>(() => result.ValueOrThrow());

        Assert.Contains("invalid_client", exception.Message, StringComparison.Ordinal);
    }
}

public sealed class ProtocolErrorsTests
{
    [Theory]
    [InlineData("invalid_request", 400)]
    [InlineData("invalid_client", 401)]
    [InlineData("invalid_grant", 400)]
    [InlineData("unsupported_grant_type", 400)]
    [InlineData("invalid_scope", 400)]
    [InlineData("unauthorized_client", 400)]
    public void LosErroresDelTokenEndpointUsanElCodigoHttpDeLaEspecificacion(string code, int expectedStatusCode)
    {
        var error = BuildError(code);

        Assert.Equal(code, error.Code);
        Assert.Equal(expectedStatusCode, error.StatusCode);
    }

    [Fact]
    public void InvalidClientRespondeUnauthorized()
    {
        Assert.Equal(401, ProtocolErrors.InvalidClient("desconocido").StatusCode);
    }

    [Fact]
    public void UnErrorDeAutorizacionTambienEsBadRequest()
    {
        var error = AuthorizationErrors.AccessDenied("el usuario rechazo");

        Assert.Equal("access_denied", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void ElErrorDescribeCodigoDescripcionYEstado()
    {
        var error = ProtocolErrors.InvalidGrant("el codigo ya fue usado");

        Assert.Equal("invalid_grant (400): el codigo ya fue usado", error.ToString());
    }

    private static ProtocolError BuildError(string code) => code switch
    {
        "invalid_request" => ProtocolErrors.InvalidRequest("falta un parametro"),
        "invalid_client" => ProtocolErrors.InvalidClient("secreto incorrecto"),
        "invalid_grant" => ProtocolErrors.InvalidGrant("codigo invalido"),
        "unsupported_grant_type" => ProtocolErrors.UnsupportedGrantType("no soportado"),
        "invalid_scope" => ProtocolErrors.InvalidScope("scope desconocido"),
        _ => ProtocolErrors.UnauthorizedClient("no permitido")
    };
}