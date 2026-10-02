using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OidcMock.ClientCompatibilityTests.Infrastructure;

/// <summary>
/// Segunda aplicacion del segundo escenario: un API protegido con JwtBearer, tambien configurado solo
/// con el issuer del mock. Las claves, el issuer y la audiencia salen del discovery, igual que en una
/// API real.
///
/// Se separa de <see cref="OidcClientHost"/> a proposito: son dos aplicaciones distintas con dos
/// esquemas distintos, y un solo host con los dos scheme seria mas dificil de leer que dos tipos.
/// </summary>
public sealed class JwtBearerApiHost : IAsyncDisposable
{
    private const string Audience = "backend-service";

    private readonly WebApplication _application;

    private JwtBearerApiHost(WebApplication application)
    {
        _application = application;
    }

    public string BaseAddress { get; private init; } = string.Empty;

    public static async Task<JwtBearerApiHost> StartAsync(string issuer, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();

        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddSimpleConsole();
        builder.WebHost.UseUrls($"http://127.0.0.1:{LoopbackAddress.Reserve().Port}");

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = issuer;
                options.Audience = Audience;

                // Sin audience el JwtBearer de .NET avisa y no valida ninguna; el token del mock la
                // lleva (es el client_id), asi que se comprueba de verdad.
                options.RequireHttpsMetadata = false;
            });

        builder.Services.AddAuthorization();

        var application = builder.Build();

        application.MapGet(ProtectedPath, (HttpContext context) =>
            Results.Json(Claims.Describe(context.User)));

        await application.StartAsync(cancellationToken);

        return new JwtBearerApiHost(application)
        {
            BaseAddress = application.Urls.First()
        };
    }

    public const string ProtectedPath = "/api/valores";

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}