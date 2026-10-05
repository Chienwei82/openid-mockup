using System.Text.Json;
using OidcMock.Core.Authorization;
using OidcMock.Core.Codes;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Core.Grants;
using OidcMock.Core.Scopes;
using OidcMock.UnitTests.Fixtures;

namespace OidcMock.UnitTests.Discovery;

public sealed class DiscoveryDocumentBuilderTests
{
    private const string Issuer = "http://localhost:5000/personafisica/";
    private const string ReferenceIssuer = "https://oauth2.bccr.fi.cr/personafisica/";

    /// <summary>
    /// El discovery se arma con listas literales dentro del propio builder, que se pueden quedar atras
    /// sin que nada falle: registrar un grant nuevo no lo anuncia y nadie se entera hasta que un
    /// cliente real lo echa de menos. Este test ata cada lista a la constante del dominio que la define.
    /// </summary>
    [Fact]
    public void LosValoresAnunciadosVienenDeLasConstantesDelDominio()
    {
        var document = BuildDocument();

        Assert.Equal(GrantTypes.Supported, document.GrantTypesSupported);
        Assert.Equal(ResponseTypeNames.SupportedCombinations, document.ResponseTypesSupported);
        Assert.Equal(ResponseModes.Supported, document.ResponseModesSupported);
        Assert.Equal(PromptValues.Supported, document.PromptValuesSupported);
        Assert.Equal(PkceCodeChallengeMethods.Supported, document.CodeChallengeMethodsSupported);
    }

    [Fact]
    public void LosNombresDeCampoSonUnSubconjuntoDelDiscoveryDeReferencia()
    {
        var unknownFields = FieldNames(BuildDocument())
            .Where(field => !ReferenceDiscoveryDocument.FieldNames.Contains(field))
            .ToArray();

        Assert.Empty(unknownFields);
    }

    [Fact]
    public void AnunciaLosMismosNombresDeRutaQueElDiscoveryDeReferencia()
    {
        var endpointFields = FieldNames(BuildDocument())
            .Where(field => field.EndsWith("_endpoint", StringComparison.Ordinal) || field.EndsWith("_uri", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "authorization_endpoint",
                "backchannel_authentication_endpoint",
                "device_authorization_endpoint",
                "end_session_endpoint",
                "introspection_endpoint",
                "jwks_uri",
                "pushed_authorization_request_endpoint",
                "revocation_endpoint",
                "token_endpoint",
                "userinfo_endpoint"
            ],
            endpointFields);
    }

    [Fact]
    public void TodosLosEndpointsCuelganDelIssuer()
    {
        var document = BuildDocument();

        Assert.Equal(Issuer, document.Issuer);
        Assert.Equal($"{Issuer}connect/token", document.TokenEndpoint);
        Assert.Equal($"{Issuer}connect/authorize", document.AuthorizationEndpoint);
        Assert.Equal($"{Issuer}.well-known/openid-configuration/jwks", document.JwksUri);
    }

    [Fact]
    public void LosNombresDeRutaCoincidenConLosDelDiscoveryDeReferencia()
    {
        var document = BuildDocument();

        Assert.Equal(
            $"{ReferenceIssuer}connect/authorize",
            document.AuthorizationEndpoint?.Replace(Issuer, ReferenceIssuer, StringComparison.Ordinal));
        Assert.Equal(
            $"{ReferenceIssuer}connect/ciba",
            document.BackchannelAuthenticationEndpoint?.Replace(Issuer, ReferenceIssuer, StringComparison.Ordinal));
    }

    [Fact]
    public void LosScopesProvienenDelStoreYLosClaimsSonSuUnion()
    {
        var document = BuildDocument();

        Assert.Equal(["openid", "profile", "roles"], document.ScopesSupported);
        Assert.Equal(["sub", "name", "role"], document.ClaimsSupported);
    }

    [Fact]
    public void AnunciaSoloLoQueElMockImplementa()
    {
        var document = BuildDocument();
        var fields = FieldNames(document);

        Assert.DoesNotContain("request_object_signing_alg_values_supported", fields);
        Assert.DoesNotContain("dpop_signing_alg_values_supported", fields);
        Assert.DoesNotContain("userinfo_signing_alg_values_supported", fields);
        Assert.DoesNotContain("introspection_signing_alg_values_supported", fields);
        Assert.DoesNotContain("request_parameter_supported", fields);
        Assert.DoesNotContain("ClientCertificate", AllStringValues(document));
    }

    [Fact]
    public void DeclaraLosValoresDeAlgoritmoYPromptDelServidorReal()
    {
        var document = BuildDocument();

        Assert.Equal(["RS256"], document.IdTokenSigningAlgValuesSupported);
        Assert.Equal(["plain", "S256"], document.CodeChallengeMethodsSupported);
        Assert.Equal(["public"], document.SubjectTypesSupported);
        Assert.Equal(["none", "login", "consent", "select_account"], document.PromptValuesSupported);
        Assert.True(document.AuthorizationResponseIssParameterSupported);
        Assert.False(document.RequirePushedAuthorizationRequests);
    }

    [Fact]
    public void CambiarElPathBaseCambiaTodasLasUrls()
    {
        var document = BuildDocument("http://localhost:5000/otro-prefijo/");

        Assert.Equal("http://localhost:5000/otro-prefijo/", document.Issuer);
        Assert.Equal("http://localhost:5000/otro-prefijo/connect/token", document.TokenEndpoint);
        Assert.Equal("http://localhost:5000/otro-prefijo/.well-known/openid-configuration/jwks", document.JwksUri);
        Assert.All(document.Paths(), path => Assert.StartsWith("http://localhost:5000/otro-prefijo/", path, StringComparison.Ordinal));
    }

    private static DiscoveryDocument BuildDocument(string? issuer = null)
    {
        var scopeStore = new InMemoryScopeStore(
        [
            new("openid", ["sub"]),
            new("profile", ["name", "sub"]),
            new("roles", ["role"])
        ]);

        return new DiscoveryDocumentBuilder(
                scopeStore,
                new OidcMockOptions(),
                ClientStoreFixture.AuthenticatorOver(ClientStoreFixture.Create()))
            .Build(issuer ?? Issuer);
    }

    private static string[] FieldNames(DiscoveryDocument document) =>
        Serialize(document).EnumerateObject().Select(property => property.Name).ToArray();

    private static string[] AllStringValues(DiscoveryDocument document) =>
        Serialize(document)
            .EnumerateObject()
            .SelectMany(property => property.Value.ValueKind == JsonValueKind.Array
                ? property.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)
                : [])
            .ToArray();

    private static JsonElement Serialize(DiscoveryDocument document) =>
        JsonSerializer.SerializeToElement(document, DiscoveryDocument.SerializerOptions);

    private sealed class InMemoryScopeStore(IReadOnlyList<ScopeDefinition> scopes) : IScopeStore
    {
        public ScopeDefinition? Find(string name) =>
            scopes.FirstOrDefault(scope => string.Equals(scope.Name, name, StringComparison.Ordinal));

        public IReadOnlyList<ScopeDefinition> List() => scopes;
    }
}
