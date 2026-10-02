# Decisiones de diseño

## Prompt 4 — Authorization Code, token endpoint y endpoints protegidos

### Estructura del dominio
- `Core/Errors`: `Result<T>` + `ProtocolError` (codigo, descripcion, status HTTP) y dos catalogos,
  `ProtocolErrors` (token endpoint) y `AuthorizationErrors` (authorize). Centralizarlos evita que el
  mismo error viaje con status distintos en dos sitios.
- `ProtocolErrors.InvalidClient` es **401** (RFC 6749 5.2) pero `AuthorizationErrors.InvalidClient`
  es **400**: en `/connect/authorize` el cliente no se autentica, solo se identifica. Copiar el status
  sin pensar rompe clientes reales..
- `Result<T>` dispara **CA1000** (no statics en tipos genericos). Se suprime con `SuppressMessage` y
  justificacion: las fabricas deben vivir en el propio `Result<T>` para poder escribirse
  `Result<TokenResponse>.Ok(...)` sin repetir el constructor privado en cada caso de uso.
- La propiedad booleana del resultado de validacion se llama `IsError`, no `Failed`, porque `Failed`
  choca con el metodo fabrica `Failed(...)` del mismo tipo.

### Autorizacion: reglas encadenadas y redireccion segura
- `AuthorizationRequestValidator` aplica una lista de `AuthorizationRule` (delegados): cada regla
  devuelve el error o `null` y se toma el primer fallo. Anadir una regla es agregar una entrada, sin
  tocar las existentes y sin `if` anidados. La lista es de **instancia** porque `ValidateScopes`
  consulta el `IScopeStore` inyectado (y un inicializador de campo no puede referenciar metodos de
  instancia, asi que el constructor es explicito).
- `AuthorizationValidationResult.CanRedirect` distingue los errores que viajan por `redirect_uri` de
  los que se muestran en el endpoint. **Solo se redirige cuando `client_id` y `redirect_uri` ya son
  validos**: si no, el endpoint seria un vector de *open redirect*.
- **PKCE sin `code_challenge_method` usa `plain`**, el default de RFC 7636 4.3 (no S256). Un challenge
  sin metodo no es error; un metodo sin challenge si lo es.
- La respuesta lleva siempre `iss` (`authorization_response_iss_parameter_supported`).

### Grants como Strategy
- `IGrantHandler` + `GrantHandlerRegistry`: el token endpoint **no conoce** los casos particulares de
  ningun grant. Agregar uno es registrar `services.AddSingleton<IGrantHandler, X>()` y nada mas.
- `TokenEndpointService` autentica al cliente y valida scopes **antes** de despachar, y sale temprano
  con el primer error. Un cliente publico (sin secreto) es valido en el token endpoint: la
  proteccion la aporta el PKCE que exigio el authorize.
- `authorization_code` verifica `redirect_uri` y `code_verifier` contra lo guardado al emitir el
  codigo (RFC 6749 4.1.3). El **codigo guarda el redirect_uri**: sin el, el canje fallaba contra el
  servidor real y solo se detectaba recorriendo el flujo completo contra el host.
- `refresh_token` **rota**: cada canje consume el token y emite uno nuevo.
- `client_credentials` no emite `id_token` ni `refresh_token`, y sin usuario no proyecta claims.
- El grant `refresh_token` **no reemite `id_token`** en el mock; `IGrantHandler.IssuesIdToken` decide
  caso por caso en lugar de asumirlo en la fabrica comun.

### `AccessTokenRequest.IssuedAt` era un campo muerto

- `AccessTokenRequest` declaraba `IssuedAt`, pero `JsonWebTokenFactory` lo **ignoraba**: tomaba el
  instante de su propio `TimeProvider` (`Now()`) y de ahi derivaba `iat`, `nbf` y `exp`. El campo viajaba
  en el request y se descartaba en silencio, que es la peor forma de API: un llamador que creyera
  estar fijando la emision firmaba el intervalo del cliente creyendo que si.
- **Decision (D-020)**: quitar el campo en vez de hacerlo cumplir. El factory es la unica fuente del
  instante de emision, coherente con la regla de que el reloj sale siempre del `TimeProvider` y con el
  refactor previo que unifico `iat`/`nbf`/`exp` en una sola lectura. Un segundo reloj en el request
  permitiria emitir un token cuyo `iat` contradiga a su `exp`.
- Con el campo fuera, `ClientCredentialsGrantHandler` ya no necesita `TimeProvider` y se le quito del
  constructor: el compilador (CS9113) aviso del parametro sin uso.

### Cobertura de los bugs encontrados

Los tres escaparon durante la implementacion. Lo que los detecta hoy:

| Bug | Detección |
|---|---|
| El codigo de autorizacion no guardaba el `redirect_uri` | Test: `ElCodigoGuardaElRedirectUriDeLaPeticionAprobada` |
| `AccessTokenReader` validaba la vida util contra el reloj del proceso | **Estructural**: CS9113 + CA1822 impiden reintroducirlo |
| `Deny` no limpiaba la aprobacion previa | Test: `DenegarlaImpideElCanjede` |

Verificado revirtiendo cada correccion: el bug 1 produce un fallo de test, y el bug 2 ni siquiera
compila. `AuthorizationService` no tenia ningun test propio, que es justo por lo que el bug 1 paso
desapercibido; ahora tiene `AuthorizationServiceTests` (credenciales, emision del codigo y todo lo que
el canje posterior necesita).

### `AccessTokenReader`
- UserInfo, introspect y revocation leen el token por un unico `IAccessTokenReader`, que valida firma,
  issuer y vida util contra el JWKS. Validar a mano en cada endpoint habria dispersado la firma.
- La vida util se comprueba con el **`TimeProvider` inyectado**, no con el reloj del proceso: un token
  emitido con un reloj de pruebas se rechazaria como caducado. Es el mismo problema que el `nbf` de la
  etapa 3 (D-015), ahora del lado de la lectura.
- `audiences: null` desactiva la comprobacion de audiencia, que es lo que necesita userinfo (su
  audiencia es el propio `client_id` del token, que todavia no se conoce). Ademas hay que poner
  `ValidateAudience = false`: con la lista vacia la libreria **lanza** en lugar de pasar.

### Introspection y revocation
- Un token desconocido, caducado o de **otro cliente** se responde `active=false` (RFC 7662 2.2), no
  con un error: el endpoint no debe filtrar por que fallo. La revocacion responde 200 igual
  (RFC 7009 2.2), aunque internamente si distingue si algo se revoco.
- El access token es un JWT sin estado: no hay nada que borrar. Se considera revocado para coherencia
  y caduca por `exp`, igual que el servidor real.

### Flujos por sondeo (device, CIBA) y PAR
- Los tres comparten `IPendingAuthorizationStore`: peticion pendiente, handle, expiracion, intervalo de
  sondeo, aprobacion y canje de un solo uso. Un store, no tres.
- `PollGrantHandler` es la base de `device_code` y CIBA: antes de canjear responde
  `authorization_pending` si el usuario no ha respondido y `access_denied` si lo denego (RFC 8628 3.5).
- El handle viaja en `device_code` (RFC 8628) o en `auth_req_id` (CIBA); el token endpoint lee
  cualquiera de los dos.
- **CIBA en el mock no pide confirmacion al usuario**: si el `login_hint` identifica a un usuario del
  store, la peticion queda aprobada de inmediato. Es el camino feliz para probar aplicaciones sin
  montar un segundo canal de usuario.
- `Deny` limpia la aprobacion previa. Marcar solo `Denied` dejaba el `Subject` puesto y el sondeo
  podia canjear igual; lo detecto el test `DenegarlaImpideElCanjede`.

### Endpoints
- `RequestValues.ReadAsync` lee query string y formulario en un solo sitio, para que userinfo acepte
  GET y POST sin duplicar. Es `async` porque `ReadFormAsync` no tiene version sincrona.
- Los endpoints son `async Task<IResult>`: leer el formulario es asincrono y bloquearlo con
  `.GetAwaiter().GetResult()` seria incorrecto.
- `client_secret_basic` se acepta en el encabezado `Authorization` y `client_secret_post` en el
  cuerpo, como anuncia el discovery.
- Los **response_mode** se resuelven en `AuthorizationResponder`: `query` (redireccion), `fragment`
  (redireccion con `#`) y `form_post` (HTML con autoenvio).
- El HTML se escapa con `HtmlEncoder`: sin el, un nombre de cliente con acentos rompia las aserciones
  de los tests de integracion y, mas importante, permitiria inyectar marcado.

### Fuera de alcance, decidido
- **frontchannel/backchannel logout (D-018)**: el discovery **no** anuncia `*_logout_supported`. El
  mock no mantiene sesion, asi que no hay estado que notificar a los clientes. `end_session` valida
  el `post_logout_redirect_uri` contra los clientes registrados y redirige. Anunciarlo sin
  implementarlo romperia clientes que esperan recibir la notificacion.
- **Response types implicitos (D-019)**: `id_token`, `token` y sus combinaciones se **validan** (el
  discovery los anuncia porque el servidor real lo hace) pero no emiten tokens en el fragmento. Solo
  el flujo `code` esta implementado; el fragmento exigiria negociacion de clave, que el mock no
  necesita para su proposito.
- **`ClientCertificate`**, request objects (`request`/`request_uri`) y DPoP siguen omitidos del
  discovery, igual que en la etapa 2 (D-013).


## Prompt 3 — Emisión de tokens (id_token y access_token)

### Dependencia añadida
- `Microsoft.Extensions.TimeProvider.Testing` **10.1.0** (solo tests) para `FakeTimeProvider`, que el
  enunciado pedía explícitamente. Es el paquete oficial de Microsoft que trae el reloj falso; no
  introducimos ningún otro. La versión 10.0.12 no existe en nuget.org, de ahí el 10.1.0.
  Producción sigue con solo el framework + `Microsoft.IdentityModel.JsonWebTokens`.

### Firma de los tokens
- `ITokenFactory` (`Core/Tokens`) devuelve **strings** ya firmados: el Host los copia tal cual a la
  respuesta, y el token es un valor opaco para el endpoint. `CreateIdToken(IdTokenRequest)` y
  `CreateAccessToken(AccessTokenRequest)` reciben records con los datos de la peticion.
- `JsonWebTokenFactory` usa `JsonWebTokenHandler.CreateToken` (**no** `System.IdentityModel.Tokens.Jwt`),
  tal como pedía el enunciado. `kid` sale de `RsaSecurityKey.KeyId`, que es el mismo `kid` que
  publica el JWKS, así que el token y el JWKS no pueden desincronizarse.
- `typ` se fija con `SecurityTokenDescriptor.TokenType`: `JWT` en el `id_token` y `at+jwt` en el
  `access_token`. Hay que **fijarlo explícitamente**: si no, ambos salen con `JWT`.
- `aud` del `id_token` es el `client_id` (string). En esta versión de la librería
  (`SecurityTokenDescriptor.Audience` es `string`, no colección) el `aud` múltiple se emite poniendo
  el claim a mano: string con una audiencia, array con varias. Es exactamente lo que hacen AAD y el
  servidor real, así que no se pierde compatibilidad.
- Los claims de usuario viajan como `JsonElement`, que la librería serializa preservando el tipo
  JSON (bool, número, array), en lugar de convertirlos a string.

### `nbf` obligatorio
- `SecurityTokenDescriptor` **rellena `nbf` con el reloj del sistema si no se indica**, aunque
  `IssuedAt` sí venga del `TimeProvider`. Con un reloj falso eso produce tokens "válidos en el
  futuro" y los tests de expiración mienten. Por eso el factory fija `NotBefore` explícitamente.
- `iat`, `nbf` y `exp` se calculan de **un solo** `GetUtcNow()` por token. Llamar al reloj tres
  veces puede dar tres instantes distintos y un `exp` incoherente con `iat`.

### `ClaimsProjector` (Strategy)
- `IClaimsProjector` + `ScopesClaimsProjector`: la decisión de qué claims salen sale de la **tabla de
  `scopes.json`** (`ScopeDefinition.Claims`), no de condicionales. Los claims declarados que el
  usuario no tiene se omiten; los no declarados nunca se emiten.
- La resolución del valor se delega en una cadena de `IUserClaimSource` (Strategy):
  `SubjectClaimSource` (para `sub`, que vive en `User.Subject` y no en su diccionario) y
  `UserDictionaryClaimSource` (el resto). Añadir un claim derivado no obliga a tocar el proyector.
- Un scope desconocido no aporta claims; un scope sin claims (`offline_access`) no filtra datos.

### `at_hash` / `c_hash`
- `TokenHash` calcula base64url de la **mitad izquierda** del SHA-256 del valor (OIDC Core 3.1.3.6).
  Como el mock firma con RS256, el hash es SHA-256 y la mitad son 16 bytes. Se calculan a partir del
  token/código ya emitido y solo se incluyen si existen.

### Verificación de los tests
- `TokenTestValidator` valida cada token emitido con el **mismo `JsonWebTokenHandler`** contra el
  JWKS que construye `JsonWebKeySetBuilder` desde la clave del mock. Validar contra la misma clave
  en memoria no probaría nada: el punto es comprobar el camino completo que recorre un cliente real.
- La vida útil se comprueba con un `LifetimeValidator` alimentado por `FakeTimeProvider`, porque
  `TokenValidationParameters` no recibe un reloj: sin él, el "reloj" del proceso decide qué es
  un token expirado.
- La expiración se afirma por el **código `IDX10230`** (`Lifetime validation failed`) y no por la
  palabra "expired", que no aparece en el mensaje real de la librería.

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
