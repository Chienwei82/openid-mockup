# Decisiones de diseño

## Prompt 2 — Discovery, JWKS y clave de firma

### Opciones del mock
- `OidcMockOptions` (en `Core`, sin ASP.NET) lleva `PathBase` (por defecto `/personafisica`) e
  `Issuer`. El enlace con `IConfiguration` queda en el Host (`OidcMock:PathBase`, `OidcMock:Issuer`),
  para no meter `Microsoft.Extensions.Configuration` en el dominio.
- `EndpointUri` centraliza la normalización: el `PathBase` queda con una sola barra inicial y sin
  barra final, y el `issuer` **con barra final**, igual que el servidor real. Así ninguna URL se
  concatena a mano en los endpoints.
- **Issuer por defecto = el host de la petición + PathBase.** Es lo más simple que hace que el mock
  sirva un discovery coherente en `localhost`, en cualquier puerto y detrás de cualquier proxy, sin
  configuración previa. Configurado (`OidcMock:Issuer`), gana sobre el deducido y solo se le añade
  la barra final.

### Discovery: anunciar solo lo implementado
- El documento se modela como `DiscoveryDocument` (record con `[JsonPropertyName]`) y lo arma
  `DiscoveryDocumentBuilder` a partir del `IScopeStore`: `scopes_supported` con los nombres de
  `scopes.json` y `claims_supported` con la unión de sus claims (distinta y en orden de aparición).
- Se **omiten** a propósito los campos del discovery real que el mock todavía no implementa:
  `request_object_signing_alg_values_supported`, `dpop_signing_alg_values_supported`,
  `userinfo_signing_alg_values_supported`, `introspection_signing_alg_values_supported`,
  `request_parameter_supported`, los cuatro `*_logout_supported` y `ClientCertificate` en los
  métodos de autenticación de cliente. Se agregarán en su etapa correspondiente. La regla de oro:
  **el discovery no puede anunciar capacidades inexistentes**, porque una app que las lea fallaría
  más tarde y en un punto más difícil de depurar.
- El test de *forma* (`ReferenceDiscoveryDocument`) fija el JSON del servidor real en el proyecto de
  pruebas y exige que las claves del mock sean un **subconjunto** de él: si mañana el BCCR renombra
  un campo, el test falla en lugar de dejar al cliente descubriéndolo en runtime.

### Clave de firma
- `ISigningKeyProvider` (Core) + `PemSigningKeyProvider` (Host) sobre `config/signing-key.pem`
  (PKCS#8). Si el archivo no existe genera una RSA 2048 y la persiste; por eso los tokens siguen
  validando tras reiniciar.
- `kid` = base64url(SHA-256(SubjectPublicKeyInfo)): depende solo de la clave pública, así que es
  estable entre reinicios y cambia si la clave cambia. Verificado además contra un host real
  arrancado dos veces.
- `signing-key.pem` está en `.gitignore`: es material generado en el primer arranque, no fuente.
- `JsonWebKeySetBuilder` es `static` (el analizador lo marca con CA1822 y no tiene estado de instancia);
  el `kid` se calcula en cada request a partir de la clave, lo cual es barato y evita estado cacheado.

### Nota sobre el arranque
- `app.MapDiscoveryEndpoints()` se registra **después** de `IConfigurationValidator.Validate()`, que
  sigue lanzando `ConfigurationException` antes de `app.Run()`: la configuración rota se detecta
  aunque el PathBase apunte a rutas que sí existen.

## Prompt 1 — Scaffolding y capa de datos (JSON stores)

### Estructura y paquetes
- `xunit.v3` se fijó en **3.2.0** (3.1.5 no existe en nuget.org); `xunit.runner.visualstudio` en 3.1.5.
- Los proyectos de prueba usan `OutputType=Exe` (requisito de xunit.v3) y
  `TestingPlatformDotnetTestSupport=true` para que `dotnet test` funcione con Microsoft.Testing.Platform.
- `public partial class Program` en `OidcMock.Host` para que `WebApplicationFactory<Program>` pueda arrancar el host.

### Ubicación de las pruebas
- `OidcMock.UnitTests` referencia solo `OidcMock.Core` (dominio puro).
- Las pruebas de los stores JSON viven en `OidcMock.IntegrationTests` porque el código que prueban
  (`JsonClientStore`, `JsonUserStore`, `JsonScopeStore`, `AddJsonStores`) pertenece a `OidcMock.Host`.
  Así `OidcMock.Core` mantiene la regla de no referenciar ASP.NET y el grafo de proyectos sigue mínimo.

### Stores
- `Find(...)` devuelve `null` cuando no existe (lo pidió explícitamente el enunciado) en lugar de un
  `Result<T>`; `Result<T>` se reservará para errores de negocio de los casos de uso (authorize, token, etc.).
- `reloadOnChange` se implementa **sin** `FileSystemWatcher`: cada acceso lee el archivo y compara
  sus **bytes** con los de la última carga; si difieren, se deserializa y se vuelve a mapear a
  dominio. Es determinista, sin hilos ni eventos, y detecta cambios del mismo tamaño que conservan
  la marca de tiempo (el sello `(LastWriteTimeUtc, Length)` no los detectaba). Con
  `reloadOnChange=false` el archivo se lee una sola vez y no se vuelve a tocar el disco.
- El loader (`JsonFileLoader<TFile, TDomain>`) cachea el **dominio ya mapeado**, no el DTO: recibe el
  mapper como `Func<TFile, TDomain>` y dos consultas consecutivas devuelven la misma instancia.
  Todo el `Load()` va bajo `System.Threading.Lock`.
- Un error de configuración lanza `ConfigurationException` (Core) cuyo mensaje incluye el nombre del
  archivo y el motivo; `IConfigurationValidator` fuerza la lectura de los tres archivos en el arranque
  (*fail fast*), invocado desde `Program.cs` antes de `app.Run()`.

### Configuración del host
- `OidcMock:ConfigDirectory` y `OidcMock:ReloadOnChange` permiten forzar el directorio de
  configuración y desactivar la recarga (pruebas de arranque con `WebApplicationFactory`, despliegues).
  Si no se define `ConfigDirectory`, gana la resolución de `HostConfigDirectory`.

### Ejecutar las pruebas
- `dotnet test` **a nivel de solución** aborta en este entorno con `Internal CLR error (0x80131506)`
  al coordinar los dos ejecutables vía Microsoft.Testing.Platform. A nivel de proyecto funciona:

```bash
dotnet test tests/OidcMock.UnitTests/OidcMock.UnitTests.csproj
dotnet test tests/OidcMock.IntegrationTests/OidcMock.IntegrationTests.csproj
```

### Claims de los usuarios
- `User.Claims` es `IReadOnlyDictionary<string, JsonElement>`: los nombres son libres (incluidos
  `codTipoId`, `Bccr.IdUsuario`, etc.) y los valores conservan su tipo JSON (bool, número, arreglo),
  necesario para serializar después los id_token sin pérdida.

### Directorio de configuración en ejecución
- `HostConfigDirectory.Resolve` busca, en orden: `<ContentRoot>/config`, `<ContentRoot>/../../config`
  (el `config/` del repositorio) y `<BaseDirectory>/config` (publicación). Así editar `config/*.json`
  afecta al ejecutar con `dotnet run` sin recompilar, y el binario publicado sigue siendo autónomo.

### Formato de los archivos
- Raíz con clave: `{"clients": [...]}`, `{"users": [...]}`, `{"scopes": [...]}` (permite agregar
  metadatos futuros sin romper el formato).
- Las vigencias se expresan como `TimeSpan` en formato `hh:mm:ss` (`"access_token": "00:30:00"`).
- Nombres de campos en `snake_case` (`client_id`, `redirect_uris`, `token_lifetimes`), que es lo habitual
  en archivos de configuración a mano.
- `client_secret` en texto plano, tal como se pidió (los secretos de `users.json` también, ya que el
  mock es solo para desarrollo local y offline).
