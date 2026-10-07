using Microsoft.Extensions.Configuration;
using OidcMock.Core.Configuration;

namespace OidcMock.Host;

/// <summary>
/// Decide si el mock configura Kestrel. Bajo IIS no debe: el enlace y el puerto los da el modulo de
/// ASP.NET Core (ANCM) y fijar los puertos a mano dejaria al modulo sin alcanzar la app, porque este
/// publica en un puerto aleatorio y se lo comunica al proceso. Se detecta por las variables que ANCM
/// inyecta al arrancar.
/// </summary>
public static class ServingListener
{
    public static bool ShouldConfigureKestrel(ServingOptions serving, bool hostedByIis)
    {
        ArgumentNullException.ThrowIfNull(serving);

        return !hostedByIis && (serving.UseHttps || serving.AllowHttp);
    }

    /// <summary>
    /// El modulo inyecta <c>ASPNETCORE_PORT</c> (hosting out-of-process) y variables
    /// <c>ASPNETCORE_IIS_*</c> (in-process); su presencia delata el hosting en IIS.
    /// </summary>
    public static bool IsHostedByIis(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return !string.IsNullOrEmpty(configuration["ASPNETCORE_PORT"])
            || !string.IsNullOrEmpty(configuration["ASPNETCORE_IIS_PHYSICAL_PATH"]);
    }
}
