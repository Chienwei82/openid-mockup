using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using OidcMock.Core.Codes;
using OidcMock.Core.Crypto;
using OidcMock.Core.Errors;
using OidcMock.Core.Grants;
using OidcMock.Core.Tokens;
using OidcMock.Core.Users;
using OidcMock.UnitTests.Fixtures;
using OidcMock.UnitTests.Tokens;

namespace OidcMock.UnitTests.Grants;

/// <summary>
/// Base de las pruebas de grants: arma un ITokenFactory y un validador con la MISMA clave, para que
/// los tokens emitidos se validen contra el JWKS que el mock publicaria.
/// </summary>
public abstract class GrantHandlerTestBase
{
    protected const string Issuer = "https://localhost:5001/personafisica/";
    protected const string RedirectUri = "https://localhost:5173/callback";
    protected const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    protected static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RSA _signingKey = RSA.Create(SigningKeySizes.KeySizeInBits);

    /// <summary>Misma clave con la que firma Tokens, para poder leer y validar lo emitido.</summary>
    protected RSA SigningKey => _signingKey;

    /// <summary>Proveedor de la clave de firma, para montar lectores de token en las pruebas.</summary>
    protected ISigningKeyProvider SigningKeyProvider => _signingKeyProvider;

    private readonly ISigningKeyProvider _signingKeyProvider;

    protected GrantHandlerTestBase()
    {
        Clock = new FakeTimeProvider(Now);
        CodeStore = new InMemoryCodeStore(Clock);
        RefreshTokens = new InMemoryRefreshTokenStore(Clock);
        UserStore = new InMemoryUserStore(SampleUser());
        Validator = new TokenTestValidator(_signingKey);
        _signingKeyProvider = new TestSigningKeyProvider(new SigningKey(_signingKey));
        Tokens = new JsonWebTokenFactory(
            _signingKeyProvider,
            new Core.Claims.ScopesClaimsProjector(ScopeStoreFixture.Create()),
            Clock);
    }

    protected FakeTimeProvider Clock { get; }

    protected ICodeStore CodeStore { get; }

    protected IRefreshTokenStore RefreshTokens { get; }

    protected IUserStore UserStore { get; }

    protected TokenTestValidator Validator { get; }

    protected ITokenFactory Tokens { get; }

    protected static User SampleUser() => new(
        "user-1",
        "jperez",
        "clave",
        new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["email"] = System.Text.Json.JsonSerializer.SerializeToElement("jperez@example.cr"),
            ["email_verified"] = System.Text.Json.JsonSerializer.SerializeToElement(true),
            ["full_name"] = System.Text.Json.JsonSerializer.SerializeToElement("JUAN PEREZ LOPEZ")
        });

    protected static string Sha256Base64Url(string verifier) =>
        Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    protected static AuthorizationCode IssueCode(
        ICodeStore codeStore,
        string clientId = "web-app-spa",
        string userName = "jperez",
        string? nonce = null,
        bool withChallenge = false,
        IReadOnlyList<string>? scopes = null) =>
        codeStore.Issue(new AuthorizationCodeRequest(
            clientId,
            userName,
            scopes ?? ["openid", "email"],
            nonce,
            "st-1",
            withChallenge ? Sha256Base64Url(Verifier) : null,
            withChallenge ? PkceCodeChallengeMethods.Sha256 : null,
            TimeSpan.FromMinutes(5),
            RedirectUri));

    private sealed class TestSigningKeyProvider(SigningKey key) : ISigningKeyProvider
    {
        public SigningKey GetSigningKey() => key;
    }
}

public sealed class InMemoryUserStore(params User[] users) : IUserStore
{
    public User? FindByUserName(string userName) =>
        users.FirstOrDefault(user => string.Equals(user.UserName, userName, StringComparison.Ordinal));

    public User? FindBySubject(string subject) =>
        users.FirstOrDefault(user => string.Equals(user.Subject, subject, StringComparison.Ordinal));

    public IReadOnlyList<User> List() => users;
}