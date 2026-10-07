using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using OidcMock.Core.Configuration;
using OidcMock.Core.Discovery;
using OidcMock.Host;
using OidcMock.Host.Cors;
using OidcMock.Host.Endpoints;
using OidcMock.Host.Errors;
using OidcMock.Host.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJsonStores(ResolveStoreOptions(builder));
builder.Services.AddOidcMock(builder.Configuration.GetSection(HostConfigDirectory.SectionName));
builder.Services.AddOidcMockProtocol();
builder.Services.AddOidcMockCors();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnexpectedErrorHandler>();

ConfigureListening(builder);

var app = builder.Build();

app.Services.GetRequiredService<IConfigurationValidator>().Validate();

LogServingConfiguration(app);

// La base de rutas la aporta UsePathBase: en IIS ya la pone el propio IIS (UsePathBase no hace nada
// si el path no empieza por ella), y en Kestrel recorta el prefijo configurado. Con esto el mock
// responde igual en /personafisica (local) y en /oidc (ruta relativa en IIS).
var pathBase = EndpointUri.NormalizePathBase(app.Services.GetRequiredService<OidcMockOptions>().EffectivePathBase);
if (pathBase != "/")
{
    app.UsePathBase(pathBase);
}

app.UseExceptionHandler();
app.UseCors();

app.MapDiscoveryEndpoints();
app.MapAuthorizationEndpoints();
app.MapAccountLoginEndpoints();
app.MapHomeEndpoints();
app.MapAssetEndpoints();
app.MapTokenEndpoints();
app.MapEndSessionEndpoints();
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

/// <summary>
/// Decide en que URLs escucha el mock segun <see cref="ServingOptions"/>: HTTPS con el
/// certificado de desarrollo (lo que espera un navegador local) y, si se pide, tambien HTTP plano
/// para clientes que no aceptan un certificado autofirmado.
/// </summary>
static void ConfigureListening(WebApplicationBuilder builder)
{
    // La seccion es OidcMock:Serving, no OidcMock: leer esta ultima daria las opciones por defecto.
    var serving = builder.Configuration
        .GetSection($"{HostConfigDirectory.SectionName}:{ServingOptions.SectionName}")
        .Get<ServingOptions>() ?? new ServingOptions();

    if (!ServingListener.ShouldConfigureKestrel(serving, ServingListener.IsHostedByIis(builder.Configuration)))
    {
        return;
    }

    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        if (serving.UseHttps)
        {
            kestrel.ListenAnyIP(serving.HttpsPort, ListenOptions => UseHttps(ListenOptions, serving));
        }

        if (serving.AllowHttp)
        {
            kestrel.ListenAnyIP(serving.HttpPort);
        }
    });
}

/// <summary>
/// HTTPS con el PFX propio si se configuro, y si no con el certificado de desarrollo que genera
/// <c>dotnet dev-certs https</c>, que es el que un navegador local ya tiene en su almacen.
/// </summary>
static void UseHttps(ListenOptions options, ServingOptions serving)
{
    if (serving.CertificatePath is not null)
    {
        options.UseHttps(
            System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                serving.CertificatePath,
                serving.CertificatePassword));
    }
    else
    {
        options.UseHttps();
    }
}

static void LogServingConfiguration(WebApplication app)
{
    var options = app.Services.GetRequiredService<OidcMockOptions>();
    var store = app.Services.GetRequiredService<JsonStoreOptions>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    OidcMockLog.ConfigurationLoaded(logger, store.ConfigDirectory, store.ReloadOnChange);
    OidcMockLog.ServingStarted(logger, options.Issuer ?? "(deducido del host)", options.PathBase);
}

/// <summary>
/// Punto de entrada visible para WebApplicationFactory en las pruebas de integracion.
/// </summary>
public partial class Program;
