using System.Text.Json;
using OidcMock.Core.Configuration;

namespace OidcMock.IntegrationTests.Stores;

/// <summary>
/// Coherencia de los JSON de ejemplo: lo que los datos tienen que poder salir por el protocolo.
///
/// Son errores faciles de cometer y muy dificil de ver. El discovery anuncia los scopes de
/// <c>scopes.json</c> y el token endpoint rechaza con <c>invalid_scope</c> lo que no esta ahi; y el
/// proyector solo emite los claims que un scope concede, asi que un claim que hay en
/// <c>users.json</c> pero en ningun scope se queda invisible para siempre. En ambos casos el mock
/// arranca bien y el fallo aparece en la primera peticion que alguien hace.
/// </summary>
public sealed class ConfigCoherenceTests
{
    [Fact]
    public void TodosLosScopesPermitidosPorLosClientesExistenEnElCatalogo()
    {
        var scopes = Names("scopes.json", "name");

        var huerfanos = Elements("clients.json", "clients")
            .SelectMany(client => client.GetProperty("allowed_scopes").EnumerateArray())
            .Select(scope => scope.GetString()!)
            .Where(scope => !scopes.Contains(scope))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(huerfanos);
    }

    [Fact]
    public void TodosLosClaimsDeLosUsuariosEstanEnAlgunScope()
    {
        var proyectables = Elements("scopes.json", "scopes")
            .SelectMany(scope => scope.GetProperty("claims").EnumerateArray())
            .Select(claim => claim.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var invisibles = Elements("users.json", "users")
            .SelectMany(user => user.GetProperty("claims").EnumerateObject())
            .Select(claim => claim.Name)
            .Where(claim => !proyectables.Contains(claim))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(invisibles);
    }

    private static JsonElement[] Elements(string fileName, string rootProperty) =>
        JsonDocument
            .Parse(File.ReadAllText(RepositoryLayout.ConfigFile(fileName)))
            .RootElement
            .GetProperty(rootProperty)
            .EnumerateArray()
            .ToArray();

    private static HashSet<string> Names(string fileName, string property) =>
        Elements(fileName, rootProperty: fileName == "scopes.json" ? "scopes" : "clients")
            .Select(element => element.GetProperty(property).GetString()!)
            .ToHashSet(StringComparer.Ordinal);
}
