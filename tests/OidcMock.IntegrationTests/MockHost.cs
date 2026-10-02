using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OidcMock.Host;

namespace OidcMock.IntegrationTests;

/// <summary>
/// Host del mock para las pruebas.
/// </summary>
public static class MockHost
{
    private static readonly ConcurrentBag<WebApplicationFactory<Program>> Created = [];

    /// <summary>
    /// Levanta el mock con la recarga en caliente desactivada.
    ///
    /// Cada <see cref="WebApplicationFactory{Program}"/> deja un watcher del directorio de contenido,
    /// y la suite levanta cientos. Con la recarga en caliente ademas habia un watcher por archivo de
    /// configuracion. El limite de inotify del sistema se agotaba y los tests empezaban a fallar con
    /// errores de infraestructura que no tenian nada que ver con lo que comprobaban.
    /// </summary>
    public static WebApplicationFactory<Program> Create()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(HostConfigDirectory.ReloadOnChangeSettingName, "false");

            // WebApplicationFactory infiere el content root y monta un PhysicalFileProvider con watcher
            // para detectinglo. Fijandolo aqui se evita ese watcher: es el que agota el limite de
            // inotify cuando la suite levanta cientos de hosts, y ademas el content root correcto ya
            // lo busca el propio repositorio para los archivos de configuracion.
            builder.UseContentRoot(RepositoryLayout.ContentRoot);
        });

        Created.Add(factory);

        return factory;
    }

    /// <summary>
    /// Libera los hosts que las pruebas no liberaron.
    ///
    /// El cliente que devuelve <c>CreateClient</c> no conoce el factory que lo creo, y hay muchos tests
    /// que nunca liberan el cliente ni nada mas. Sin esto los hosts se acumulan durante toda la sesion de pruebas.
    /// Se ejecuta al terminar el ensamblado, asi que tambien cubre los tests que no llegaron a
    /// dispose por un fallo.
    /// </summary>
    public static void DisposeCreatedHosts()
    {
        while (Created.TryTake(out var factory))
        {
            factory.Dispose();
        }
    }
}
