using Microsoft.Extensions.DependencyInjection;
using OidcMock.Core.Authorization;
using OidcMock.Core.Authorization.Validators;
using OidcMock.Core.Claims;
using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Configuration;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;
using OidcMock.Core.DeviceAuthorization;
using OidcMock.Core.Grants;
using OidcMock.Core.Introspection;
using OidcMock.Core.PendingRequests;
using OidcMock.Core.PushedRequests;
using OidcMock.Core.Revocation;
using OidcMock.Core.Scopes;
using OidcMock.Core.Tokens;
using OidcMock.Core.UserInfo;
using OidcMock.Core.Users;
using OidcMock.Host.Crypto;
using OidcMock.Host.Stores;

namespace OidcMock.Host;

/// <summary>
/// Registro de los stores JSON del mock.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJsonStores(this IServiceCollection services, JsonStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConfigDirectory);

        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IClientStore>(provider => CreateClientStore(provider));
        services.AddSingleton<IUserStore>(provider => CreateUserStore(provider));
        services.AddSingleton<IScopeStore>(provider => CreateScopeStore(provider));
        services.AddSingleton<IConfigurationValidator, JsonConfigurationValidator>();

        return services;
    }

    public static IServiceCollection AddJsonStores(
        this IServiceCollection services,
        string configDirectory,
        bool reloadOnChange = true) =>
        services.AddJsonStores(new JsonStoreOptions
        {
            ConfigDirectory = configDirectory,
            ReloadOnChange = reloadOnChange
        });

    /// <summary>
    /// Registra las opciones del mock, el discovery y el proveedor de la clave de firma, que usa
    /// el mismo directorio de configuracion que los stores. Invocar despues de AddJsonStores.
    /// </summary>
    public static IServiceCollection AddOidcMock(this IServiceCollection services, OidcMockOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<DiscoveryDocumentBuilder>();
        services.AddSingleton<ISigningKeyProvider>(provider =>
            new PemSigningKeyProvider(provider.GetRequiredService<JsonStoreOptions>().ConfigDirectory));

        return services;
    }

    /// <summary>
    /// Registra los casos de uso del mock: emision de tokens, autorizacion, token endpoint y los
    /// endpoints que consumen tokens. Los stores en memoria son singleton porque el estado de los
    /// codigos y los refresh tokens debe sobrevivir entre peticiones del mismo proceso.
    /// </summary>
    public static IServiceCollection AddOidcMockProtocol(this IServiceCollection services)
    {
        services.AddSingleton<ICodeStore, InMemoryCodeStore>();
        services.AddSingleton<IAuthSessionStore, InMemoryAuthSessionStore>();
        services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
        services.AddSingleton<ITokenFactory, JsonWebTokenFactory>();
        services.AddSingleton<IAccessTokenReader, AccessTokenReader>();
        services.AddSingleton<IClaimsProjector, ScopesClaimsProjector>();
        services.AddSingleton<IAuthorizationRequestValidator, AuthorizationRequestValidator>();

        // La cadena de validacion de authorize es una lista de reglas de una sola comprobacion, en
        // el orden en que se registran. Agregar una comprobacion es una linea mas, sin tocar la
        // cadena ni el endpoint.
        services.AddSingleton<IAuthorizeRequestValidator, ClientExistsValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, RedirectUriValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, GrantTypeValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, ResponseTypeValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, OpenIdScopeValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, AllowedScopesValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, KnownScopesValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, PkceRequiredValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, CodeChallengeMethodValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, PromptValidator>();
        services.AddSingleton<IAuthorizeRequestValidator, ResponseModeValidator>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<GrantHandlerRegistry>();
        services.AddSingleton<ITokenEndpointService, TokenEndpointService>();
        services.AddSingleton<IUserInfoClaimsSource, ProjectedUserInfoClaimsSource>();
        services.AddSingleton<IUserInfoService, UserInfoService>();
        services.AddSingleton<IIntrospectionService, IntrospectionService>();
        services.AddSingleton<ITokenRevocationService, TokenRevocationService>();
        services.AddSingleton<IPendingAuthorizationStore, InMemoryPendingAuthorizationStore>();
        services.AddSingleton<IPushedAuthorizationService, PushedAuthorizationService>();
        services.AddSingleton<IDeviceAuthorizationService, DeviceAuthorizationService>();

        // Cada grant es una estrategia y la DI la resuelve por su constructor. Agregar un grant nuevo
        // es registrar una linea mas, sin tocar el dispatcher del token endpoint.
        services.AddSingleton<IGrantHandler, AuthorizationCodeGrantHandler>();
        services.AddSingleton<IGrantHandler, RefreshTokenGrantHandler>();
        services.AddSingleton<IGrantHandler, ClientCredentialsGrantHandler>();
        services.AddSingleton<IGrantHandler, PasswordGrantHandler>();
        services.AddSingleton<IGrantHandler, DeviceCodeGrantHandler>();
        services.AddSingleton<IGrantHandler, CibaGrantHandler>();

        return services;
    }

    private static JsonClientStore CreateClientStore(IServiceProvider provider) =>
        new(GetRequiredOptions(provider).ConfigDirectory, GetRequiredOptions(provider).ReloadOnChange);

    private static JsonUserStore CreateUserStore(IServiceProvider provider) =>
        new(GetRequiredOptions(provider).ConfigDirectory, GetRequiredOptions(provider).ReloadOnChange);

    private static JsonScopeStore CreateScopeStore(IServiceProvider provider) =>
        new(GetRequiredOptions(provider).ConfigDirectory, GetRequiredOptions(provider).ReloadOnChange);

    private static JsonStoreOptions GetRequiredOptions(IServiceProvider provider) =>
        provider.GetRequiredService<JsonStoreOptions>();
}
