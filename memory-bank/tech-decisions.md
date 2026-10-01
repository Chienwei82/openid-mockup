# Technical Decisions — OidcMock

Estado actual de las decisiones. El histórico con contexto vive en
[`docs/decisions.md`](../docs/decisions.md).

## D-001 — Paquetes mínimos

- **Producción:** solo framework + `Microsoft.IdentityModel.JsonWebTokens` (8.23.0).
- **Tests:** `xunit.v3` 3.2.0, `xunit.runner.visualstudio` 3.1.5, `Microsoft.NET.Test.Sdk`,
  `Microsoft.AspNetCore.Mvc.Testing`. Versiones centralizadas en `Directory.Packages.props`.
- `xunit.v3` 3.1.5 no existe en nuget.org ⇒ 3.2.0. Los proyectos de test llevan
  `OutputType=Exe` y `TestingPlatformDotnetTestSupport=true` (requisitos del runner v3).
- `public partial class Program` en el Host para que `WebApplicationFactory<Program>` arranque el host.

## D-002 — Dónde vive cada test

- `OidcMock.UnitTests` referencia **solo** `OidcMock.Core` (dominio puro).
- Los tests de los stores JSON y de la composición viven en `OidcMock.IntegrationTests`, porque
  `JsonClientStore`/`JsonUserStore`/`JsonScopeStore`/`AddJsonStores` pertenecen a `OidcMock.Host`.
  Así `Core` no referencia ASP.NET y el grafo de proyectos sigue mínimo.

## D-003 — `Find(...)` devuelve `null`

El enunciado lo pide explícitamente. `Result<T>` se reserva para errores de negocio de los casos
de uso (authorize, token, introspection…). Un `null` es un resultado válido del repositorio, no un error.

## D-004 — Recarga en caliente sin `FileSystemWatcher`

Cada consulta compara el **contenido crudo** del archivo con el de la última carga (comparación
byte a byte) y re-deserializa + re-mapea si cambió. Es determinista, sin hilos ni eventos.
`reloadOnChange: false` ⇒ lectura única, sin I/O posterior.
*Alternativas descartadas:* `FileSystemWatcher` (eventos, condiciones de carrera, no determinista en
tests) y sello `(LastWriteTimeUtc, Length)` (falla si el contenido cambia del mismo tamaño dentro de
la granularidad de timestamp del filesystem).

## D-005 — Caché del dominio, no del JSON

El loader cachea **el dominio ya mapeado** (`JsonFileLoader<TFile, TDomain>` recibe la proyección
`Func<TFile, TDomain>`), no el DTO. Así cada consulta evita tanto el `JsonSerializer` como el
mapeo, y dos llamadas consecutivas devuelven la misma instancia inmutable.

## D-006 — `reloadOnChange: false` no toca disco

Con la recarga desactivada, `Load()` devuelve la caché sin leer ni hacer `stat` del archivo: si el
archivo desaparece después de la primera carga, el store sigue sirviendo la configuración en memoria.

## D-007 — Errores de configuración: `ConfigurationException`

Tipo en `Core` con `FileName` y `Reason`; el mensaje incluye ambos. Se lanza desde el loader
(archivo ausente, JSON mal formado, formato no soportado) y desde los mappers (falta `client_id`,
`sub`, `username`, `name` o la colección raíz). `IConfigurationValidator` fuerza la lectura de los
tres archivos en el arranque ⇒ la app no arranca con configuración rota.

## D-008 — Claims de usuario con nombres libres

`User.Claims` es `IReadOnlyDictionary<string, JsonElement>`: admite `codTipoId`, `Bccr.IdUsuario`,
`Bccr.X509Subject`, etc., y conserva el tipo JSON (bool, número, **arreglo** como `role`) para
serializar después el `id_token` sin pérdida.

## D-009 — Directorio de configuración en ejecución

`HostConfigDirectory.Resolve` busca `<ContentRoot>/config`, `<ContentRoot>/../../config` y
`<AppContext.BaseDirectory>/config`. Así editar `config/*.json` afecta al ejecutar con `dotnet run`
sin recompilar, y el binario publicado sigue siendo autónomo.
`OidcMock:ConfigDirectory` y `OidcMock:ReloadOnChange` permiten forzar ambos valores desde la
configuración del host (pruebas de arranque, despliegues); sin `ConfigDirectory` gana la resolución.

## D-010 — Formato de los archivos de configuración

- Raíz con clave: `{"clients": [...]}`, `{"users": [...]}`, `{"scopes": [...]}` (admite metadatos futuros).
- Campos en `snake_case`; vigencias como `TimeSpan` `"hh:mm:ss"`.
- `client_secret` y `password` en texto plano: es un mock local y offline.
- El deserializador tolera comentarios y comas finales (`JsonConfiguration.SerializerOptions`).

## D-011 — Ejecutar los tests

`dotnet test` **a nivel de solución** aborta en este entorno con
`Internal CLR error (0x80131506)` al coordinar los dos ejecutables de test vía
Microsoft.Testing.Platform. **A nivel de proyecto funciona**, y los runners in-process también:

```bash
dotnet test tests/OidcMock.UnitTests/OidcMock.UnitTests.csproj
dotnet test tests/OidcMock.IntegrationTests/OidcMock.IntegrationTests.csproj
```
