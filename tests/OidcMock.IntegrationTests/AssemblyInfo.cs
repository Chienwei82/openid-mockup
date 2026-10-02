using Xunit;

// Cada prueba levanta un host del mock, y cada host deja un watcher del sistema. Con las pruebas en
// paralelo, la suite abre mas instancias de inotify de las que la maquina permite y los tests
// empiezan a fallar con errores de infraestructura. Sin paralelismo, ademas, los tests que comparten
// el directorio de configuracion se pisan entre si.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Libera los hosts que los tests dejaron sin dispose (el cliente de CreateClient no conoce su
// factory): sin este fixture, cada prueba filtra un host y sus watchers.
[assembly: AssemblyFixture(typeof(OidcMock.IntegrationTests.MockHostFixture))]
