using OidcMock.Core.Errors;
using OidcMock.Core.Tokens;

namespace OidcMock.Core.Grants;

/// <summary>
/// Grant client_credentials (RFC 6749 4.4): el cliente se autentica con su propio secreto y no hay
/// usuario detrás, así que el subject es el client_id y no se emiten id_token ni refresh token.
/// No necesita <c>TimeProvider</c>: el reloj lo usa el token factory al firmar.
/// </summary>
public sealed class ClientCredentialsGrantHandler(ITokenFactory tokenFactory) : IGrantHandler
{
    public string GrantType => GrantTypes.ClientCredentials;

    public bool IssuesIdToken => false;

    public Task<Result<TokenResponse>> HandleAsync(TokenRequest request) =>
        Task.FromResult(Handle(request));

    private Result<TokenResponse> Handle(TokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accessToken = tokenFactory.CreateAccessToken(new AccessTokenRequest(
            request.Issuer,
            request.Client.ClientId,
            request.Scopes,
            [request.Client.ClientId],
            request.Client.ClientId,
            null,
            request.Client.TokenLifetimes.AccessToken));

        return Result<TokenResponse>.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = (long)request.Client.TokenLifetimes.AccessToken.TotalSeconds,
            Scope = string.Join(' ', request.Scopes)
        });
    }
}