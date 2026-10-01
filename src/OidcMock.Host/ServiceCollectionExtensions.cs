using Microsoft.Extensions.DependencyInjection;
using OidcMock.Core.Clients;
using OidcMock.Core.Configuration;
using OidcMock.Core.Scopes;
using OidcMock.Core.Users;
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

    private static JsonClientStore CreateClientStore(IServiceProvider provider) =>
        new(GetRequiredOptions(provider).ConfigDirectory, GetRequiredOptions(provider).ReloadOnChange);

    private static JsonUserStore CreateUserStore(IServiceProvider provider) =>
        new(GetRequiredOptions(provider).ConfigDirectory, GetRequiredOptions(provider).ReloadOnChange);

    private static JsonScopeStore CreateScopeStore(IServiceProvider provider) =>
        new(GetRequiredOptions(provider).ConfigDirectory, GetRequiredOptions(provider).ReloadOnChange);

    private static JsonStoreOptions GetRequiredOptions(IServiceProvider provider) =>
        provider.GetRequiredService<JsonStoreOptions>();
}
