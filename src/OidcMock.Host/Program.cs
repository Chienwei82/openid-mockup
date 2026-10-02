using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Host;
using OidcMock.Host.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJsonStores(ResolveStoreOptions(builder));
builder.Services.AddOidcMock(ResolveOidcMockOptions(builder));
builder.Services.AddOidcMockProtocol();

var app = builder.Build();

app.Services.GetRequiredService<IConfigurationValidator>().Validate();

app.MapDiscoveryEndpoints();
app.MapAuthorizationEndpoints();
app.MapTokenEndpoints();
app.MapPushedRequestEndpoints();
app.MapPollEndpoints();

app.Run();

static JsonStoreOptions ResolveStoreOptions(WebApplicationBuilder builder)
{
    var reloadOnChange = builder.Configuration.GetValue(HostConfigDirectory.ReloadOnChangeSettingName, defaultValue: true);
    var configuredDirectory = builder.Configuration[HostConfigDirectory.ConfigDirectorySettingName];

    return configuredDirectory is null
        ? HostConfigDirectory.DefaultOptions(builder.Environment, reloadOnChange)
        : new JsonStoreOptions { ConfigDirectory = configuredDirectory, ReloadOnChange = reloadOnChange };
}

static OidcMockOptions ResolveOidcMockOptions(WebApplicationBuilder builder) => new()
{
    PathBase = builder.Configuration[HostConfigDirectory.PathBaseSettingName] ?? OidcMockOptions.DefaultPathBase,
    Issuer = builder.Configuration[HostConfigDirectory.IssuerSettingName]
};

/// <summary>
/// Punto de entrada visible para WebApplicationFactory en las pruebas de integracion.
/// </summary>
public partial class Program;
