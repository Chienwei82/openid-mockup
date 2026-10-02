using OidcMock.Core.Claims;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;
using OidcMock.Host.Crypto;
using OidcMock.Host.Stores;

namespace OidcMock.IntegrationTests.Endpoints;

/// <summary>
/// Mina access tokens con la MISMA clave que el host usa (config/signing-key.pem), para poder construir
/// por HTTP los casos que el flujo normal no produce: un token ya caducado, o uno de otro cliente.
/// <para>
/// Se fija el instante de emision en vez de esperar a que el token caduque: un test que dependa de
/// Task.Delay seria lento y flaky, y el mock tiene que poder probar la caducidad sin dormir.
/// </para>
/// </summary>
public sealed class ExpiredTokenMinter : IDisposable
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(30);
    private static readonly string Subject = "user-persona-fisica";

    private readonly PemSigningKeyProvider _keyProvider;
    private readonly JsonWebTokenFactory _expiredFactory;
    private readonly JsonWebTokenFactory _validFactory;

    public ExpiredTokenMinter(string issuer, string clientId)
    {
        Issuer = issuer;
        ClientId = clientId;

        _keyProvider = new PemSigningKeyProvider(RepositoryLayout.ConfigDirectory);
        var claimsProjector = new ScopesClaimsProjector(
            new JsonScopeStore(RepositoryLayout.ConfigDirectory));

        // Emitido en el pasado: su exp ya llego cuando el host lo lea con el reloj del proceso.
        _expiredFactory = new JsonWebTokenFactory(
            _keyProvider,
            claimsProjector,
            new FixedTimeProvider(DateTimeOffset.UtcNow - (AccessTokenLifetime * 2)));

        _validFactory = new JsonWebTokenFactory(_keyProvider, claimsProjector, TimeProvider.System);
    }

    /// <summary>Issuer tal como el host lo resolveria para esta peticion.</summary>
    public static string IssuerFor(HttpClient client, string pathBase) =>
        $"{client.BaseAddress!.Scheme}://{client.BaseAddress.Authority}{pathBase}/";

    public string Issuer { get; }

    public string ClientId { get; }

    /// <summary>Access token correctamente firmado pero ya caducado.</summary>
    public string Expired() => _expiredFactory.CreateAccessToken(Request());

    /// <summary>Access token vigente, para el caso de que el problema no sea la caducidad.</summary>
    public string Valid() => _validFactory.CreateAccessToken(Request());

    public void Dispose() => _keyProvider.Dispose();

    private AccessTokenRequest Request() => new(
        Issuer,
        ClientId,
        ["openid", "email"],
        [ClientId],
        Subject,
        User(),
        AccessTokenLifetime);

    private static User User() => new(
        Subject,
        "jperez",
        "Passw0rd!",
        new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["email"] = System.Text.Json.JsonSerializer.SerializeToElement("jperez@example.cr"),
            ["email_verified"] = System.Text.Json.JsonSerializer.SerializeToElement(true)
        });

    /// <summary>Reloj fijo, para que "emitido en el pasado" sea un hecho y no una carrera.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}