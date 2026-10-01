using OidcMock.Core.Configuration;
using OidcMock.Host;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJsonStores(ResolveStoreOptions(builder));

var app = builder.Build();

app.Services.GetRequiredService<IConfigurationValidator>().Validate();

app.Run();

static JsonStoreOptions ResolveStoreOptions(WebApplicationBuilder builder)
{
    var reloadOnChange = builder.Configuration.GetValue(HostConfigDirectory.ReloadOnChangeSettingName, defaultValue: true);
    var configuredDirectory = builder.Configuration[HostConfigDirectory.ConfigDirectorySettingName];

    return configuredDirectory is null
        ? HostConfigDirectory.DefaultOptions(builder.Environment, reloadOnChange)
        : new JsonStoreOptions { ConfigDirectory = configuredDirectory, ReloadOnChange = reloadOnChange };
}

/// <summary>
/// Punto de entrada visible para WebApplicationFactory en las pruebas de integracion.
/// </summary>
public partial class Program;
