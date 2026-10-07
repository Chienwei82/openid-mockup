using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OidcMock.Core.Claims;
using OidcMock.Core.Crypto;
using OidcMock.Core.Users;

namespace OidcMock.Core.Tokens;

/// <summary>
/// Emite los tokens del mock con JsonWebTokenHandler y la clave que publica el JWKS. El instante
/// de emision y de expiracion sale siempre del TimeProvider inyectado, nunca del reloj del sistema,
/// para que los tokens sean deterministas y verificables en las pruebas.
/// </summary>
public sealed class JsonWebTokenFactory : ITokenFactory
{
    /// <summary>
    /// Valores de los claims de autenticacion que el servidor real emite siempre en el id_token y
    /// que la captura de GAUDI confirma: el mock los fija porque no autentica de otra forma, y sin
    /// ellos una aplicacion que los lea veria una diferencia entre el mock y el servidor real.
    /// </summary>
    private const string LocalIdentityProvider = "local";
    private const string PossessionOrInherence = "possessionorinherence";
    private const string SmartcardAuthentication = "sc";

    private readonly ISigningKeyProvider _signingKeyProvider;
    private readonly IClaimsProjector _claimsProjector;
    private readonly TimeProvider _timeProvider;
    private readonly JsonWebTokenHandler _handler = new();

    public JsonWebTokenFactory(
        ISigningKeyProvider signingKeyProvider,
        IClaimsProjector claimsProjector,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(signingKeyProvider);
        ArgumentNullException.ThrowIfNull(claimsProjector);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _signingKeyProvider = signingKeyProvider;
        _claimsProjector = claimsProjector;
        _timeProvider = timeProvider;
    }

    public string CreateIdToken(IdTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _handler.CreateToken(BuildIdTokenDescriptor(request));
    }

    public string CreateAccessToken(AccessTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _handler.CreateToken(BuildAccessTokenDescriptor(request));
    }

    private SecurityTokenDescriptor BuildIdTokenDescriptor(IdTokenRequest request)
    {
        var issuedAt = Now();
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [ProtocolClaimNames.Subject] = request.User.Subject,
            [ProtocolClaimNames.AuthTime] = ToEpochSeconds(request.AuthenticationTime),
            [ProtocolClaimNames.IdentityProvider] = LocalIdentityProvider,
            [ProtocolClaimNames.AuthenticationContext] = PossessionOrInherence,
            [ProtocolClaimNames.AuthenticationMethods] = new[] { SmartcardAuthentication }
        };

        AddWhenPresent(claims, ProtocolClaimNames.Nonce, request.Nonce);
        AddWhenPresent(claims, ProtocolClaimNames.SessionId, request.SessionId);
        AddHashWhenPresent(claims, ProtocolClaimNames.AccessTokenHash, request.AccessToken, TokenHash.FromAccessToken);
        AddHashWhenPresent(claims, ProtocolClaimNames.AuthorizationCodeHash, request.AuthorizationCode, TokenHash.FromAuthorizationCode);
        AddProjectedUserClaims(claims, request.User, request.GrantedScopes);

        return new SecurityTokenDescriptor
        {
            Issuer = request.Issuer,
            Audience = request.ClientId,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt + request.Lifetime,
            Claims = claims,
            TokenType = TokenHeaderValues.IdTokenType,
            SigningCredentials = SigningCredentials()
        };
    }

    private SecurityTokenDescriptor BuildAccessTokenDescriptor(AccessTokenRequest request)
    {
        var issuedAt = Now();
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [ProtocolClaimNames.Audience] = ToAudienceClaim(request.Audiences),
            [ProtocolClaimNames.Subject] = request.Subject,
            [ProtocolClaimNames.ClientId] = request.ClientId,
            [ProtocolClaimNames.Scope] = string.Join(' ', request.GrantedScopes),
            [ProtocolClaimNames.TokenId] = TokenId.New()
        };

        AddProjectedUserClaims(claims, request.User, request.GrantedScopes);

        return new SecurityTokenDescriptor
        {
            Issuer = request.Issuer,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = issuedAt + request.Lifetime,
            Claims = claims,
            TokenType = TokenHeaderValues.AccessTokenType,
            SigningCredentials = SigningCredentials()
        };
    }

    private void AddProjectedUserClaims(
        Dictionary<string, object> claims,
        User? user,
        IReadOnlyList<string> grantedScopes)
    {
        if (user is null)
        {
            return;
        }

        foreach (var (name, value) in _claimsProjector.Project(user, grantedScopes))
        {
            claims[name] = value;
        }
    }

    private static void AddWhenPresent(Dictionary<string, object> claims, string claimName, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            claims[claimName] = value;
        }
    }

    private static void AddHashWhenPresent(
        Dictionary<string, object> claims,
        string claimName,
        string? value,
        Func<string, string> hash)
    {
        if (!string.IsNullOrEmpty(value))
        {
            claims[claimName] = hash(value);
        }
    }

    /// <summary>
    /// CryptoProviderFactory propio y sin cache. IdentityModel cachea los proveedores de firma por
    /// kid, asi que dos instancias del mock con la misma clave (dos pruebas, dos WebApplicationFactory)
    /// comparten la RSA de la primera: al liberarla, la segunda falla con ObjectDisposedException al
    /// firmar. Desactivar la cache evita el cruce.
    /// </summary>
    private static readonly CryptoProviderFactory SigningCryptoProviderFactory = new()
    {
        CacheSignatureProviders = false
    };

    private SigningCredentials SigningCredentials()
    {
        var signingKey = _signingKeyProvider.GetSigningKey();

        return new SigningCredentials(
            new RsaSecurityKey(signingKey.Key)
            {
                KeyId = signingKey.KeyId,
                CryptoProviderFactory = SigningCryptoProviderFactory
            },
            TokenHeaderValues.SignatureAlgorithm);
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private static object ToAudienceClaim(IReadOnlyList<string> audiences) =>
        audiences.Count == 1 ? audiences[0] : audiences.ToArray();

    private static long ToEpochSeconds(DateTimeOffset instant) => instant.ToUnixTimeSeconds();
}