using OidcMock.Core.Configuration;
using OidcMock.Core.Errors;
using OidcMock.Core.Network;
using OidcMock.Core.Tokens;

namespace OidcMock.Core.Grants;

/// <summary>
/// Grant client_credentials (RFC 6749 4.4): el cliente se autentica con su propio secreto y no hay
/// usuario detras, así que el subject segun <see cref="SubjectPolicy"/> es el client_id o la IP
/// del llamante, y no se emiten id_token ni refresh token.
/// Solo lo admiten clientes confidenciales: un cliente publico no tiene secreto con el que
/// autenticarse, asi que no puede probarse su identidad.
/// No necesita <c>TimeProvider</c>: el reloj lo usa el token factory al firmar.
/// </summary>
public sealed class ClientCredentialsGrantHandler(
    ITokenFactory tokenFactory,
    IIpResolver ipResolver,
    SubjectPolicy subjectPolicy) : IGrantHandler
{
    public string GrantType => GrantTypes.ClientCredentials;

    public bool IssuesIdToken => false;

    public Task<Result<TokenResponse>> HandleAsync(TokenRequest request) =>
        Task.FromResult(Handle(request));

    private Result<TokenResponse> Handle(TokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Client.RequireClientSecret)
        {
            return Result<TokenResponse>.Fail(ProtocolErrors.InvalidClient(
                $"'{request.Client.ClientId}' es un cliente publico y no puede usar client_credentials."));
        }

        var subject = ResolveSubject(request.Client.ClientId);

        var accessToken = tokenFactory.CreateAccessToken(new AccessTokenRequest(
            request.Issuer,
            request.Client.ClientId,
            request.Scopes,
            [request.Client.ClientId],
            subject,
            null,
            request.Client.TokenLifetimes.AccessToken));

        return Result<TokenResponse>.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = (long)request.Client.TokenLifetimes.AccessToken.TotalSeconds,
            Scope = string.Join(' ', request.Scopes)
        });
    }

    private string ResolveSubject(string clientId) =>
        subjectPolicy switch
        {
            SubjectPolicy.IpAddress => ipResolver.Resolve() ?? string.Empty,
            _ => clientId
        };
}
