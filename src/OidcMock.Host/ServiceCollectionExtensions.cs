using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OidcMock.Core.Authorization;
using OidcMock.Core.Authorization.Validators;
using OidcMock.Core.Claims;
using OidcMock.Core.Clients;
using OidcMock.Core.Codes;
using OidcMock.Core.Configuration;
using OidcMock.Core.Crypto;
using OidcMock.Core.Discovery;
using OidcMock.Core.DeviceAuthorization;
using OidcMock.Core.EndSession;
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
using OidcMock.Host.Configuration;
using OidcMock.Host.Endpoints;
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

        services.AddSingleton<IClientStore>(provider => CreateStore<JsonClientStore>(provider));
        services.AddSingleton<IScopeStore>(provider => CreateStore<JsonScopeStore>(provider));
        services.AddSingleton<IConfigurationValidator, JsonConfigurationValidator>();

        // La pantalla de identidad crea usuarios sinteticos en memoria que sombrean los de
        // users.json: el store publico es el decorador, y los dos contratos apuntan al mismo objeto.
        services.AddSingleton(provider => new OverlayUserStore(CreateStore<JsonUserStore>(provider)));
        services.AddSingleton<IUserStore>(provider => provider.GetRequiredService<OverlayUserStore>());
        services.AddSingleton<IUserOverlay>(provider => provider.GetRequiredService<OverlayUserStore>());

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
    /// Registra las opciones del mock desde la seccion de configuracion, con validacion al arrancar
    /// (<c>ValidateOnStart</c>), el discovery y el proveedor de la clave de firma, que usa el mismo
    /// directorio de configuracion que los stores. Invocar despues de AddJsonStores.
    /// </summary>
    public static IServiceCollection AddOidcMock(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<OidcMockOptions>, OidcMockOptionsValidation>();
        services.AddOptions<OidcMockOptions>()
            .Bind(configuration)
            // El enlace de configuracion anade elementos a una lista ya poblada en vez de
            // reemplazarla, asi que la lista nace vacia y los origenes por defecto se aplican
            // despues, solo si la configuracion no trajo ninguno.
            .PostConfigure(ApplyDefaultCorsOrigins)
            .ValidateOnStart();
        // El resto del codigo pide OidcMockOptions por su clase; se resuelve el mismo objeto ya validado.
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<OidcMockOptions>>().Value);
        services.AddSingleton<DiscoveryDocumentBuilder>();
        services.AddSingleton<ISigningKeyProvider>(provider => new PemSigningKeyProvider(
            provider.GetRequiredService<JsonStoreOptions>().ConfigDirectory,
            ConfigurationFiles.SigningKey,
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<PemSigningKeyProvider>()));

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
        services.AddSingleton<ITokenRevocationStore, InMemoryTokenRevocationStore>();
        services.AddSingleton<ITokenFactory, JsonWebTokenFactory>();
        services.AddSingleton<SignedTokenValidator>();
        services.AddSingleton<IAccessTokenReader, AccessTokenReader>();
        services.AddSingleton<IIdTokenReader, IdTokenReader>();
        services.AddSingleton<IEndSessionService, EndSessionService>();
        services.AddSingleton<IClaimsProjector, ScopesClaimsProjector>();
        services.AddSingleton<IAuthorizationRequestValidator, AuthorizationRequestValidator>();
        services.AddSingleton<AuthorizationBinder>();

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
        services.AddSingleton<IAuthorizationInteraction, AuthorizationInteraction>();
        services.AddSingleton<IConsentStore, InMemoryConsentStore>();
        services.AddSingleton<GrantHandlerRegistry>();
        services.AddSingleton<ITokenEndpointService, TokenEndpointService>();

        // La autenticacion de cliente es una estrategia por metodo, y el coordinador elige. Agregar
        // un metodo (private_key_jwt, mTLS) es registrar otra linea aqui.
        services.AddSingleton<IClientAuthenticator, ClientSecretBasicAuthenticator>();
        services.AddSingleton<IClientAuthenticator, ClientSecretPostAuthenticator>();
        services.AddSingleton<ClientAuthenticator>();
        services.AddSingleton<IUserInfoClaimsSource, ProjectedUserInfoClaimsSource>();
        services.AddSingleton<IUserInfoService, UserInfoService>();
        services.AddSingleton<IIntrospectionService, IntrospectionService>();
        services.AddSingleton<ITokenRevocationService, TokenRevocationService>();
        services.AddSingleton<IPendingAuthorizationStore, InMemoryPendingAuthorizationStore>();
        services.AddSingleton<IPushedAuthorizationService, PushedAuthorizationService>();
        services.AddSingleton<IPollAuthorizationService, PollAuthorizationService>();

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

    private static void ApplyDefaultCorsOrigins(OidcMockOptions options)
    {
        if (options.AllowedCorsOrigins.Count == 0)
        {
            options.AllowedCorsOrigins = OidcMockOptions.DefaultAllowedCorsOrigins;
        }
    }

    /// <summary>
    /// Crea un store JSON con el directorio y la recarga de las opciones. Los tres se construyen igual y
    /// solo cambia el tipo, asi que un metodo generico evita tres copias que se pueden desincronizar.
    /// </summary>
    private static TStore CreateStore<TStore>(IServiceProvider provider)
        where TStore : class
    {
        var options = GetRequiredOptions(provider);

        return (TStore)Activator.CreateInstance(typeof(TStore), options.ConfigDirectory, options.ReloadOnChange)!;
    }

    private static JsonStoreOptions GetRequiredOptions(IServiceProvider provider) =>
        provider.GetRequiredService<JsonStoreOptions>();
}
