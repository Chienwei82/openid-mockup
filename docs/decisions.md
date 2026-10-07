# Decisiones de diseño

## Prompt 8 — userinfo, introspect y revocation con un store de revocaciones compartido

### Un solo `ITokenRevocationStore` para userinfo, introspect, revocation y los grants
- El access token del mock es un JWT sin estado: no hay nada que "borrar" en el token. Antes de esta
  etapa `TokenRevocationService` **no revocaba access tokens en absoluto** (devolvia `true` y seguian
  sirviendo hasta el `exp`), asi que revocar era decorativo. Ahora se registra el `jti`.
- El store se indexa por `jti` (access tokens) y por `family_id` (refresh tokens). Cada entrada guarda
  el `exp` del token al que corresponde y se descarta cuando ese instante pasa: a partir de ahi la
  revocacion es indistinguible de un token caducado, asi que guardarla seria memoria inutil.
- Lo consultan los tres casos de uso y el grant `refresh_token`. En el handler es una **segunda puerta**:
  el store de refresh tokens ya habria fallado al canjear, pero si la revocacion llego por otra via, el
  store de revocaciones manda. Se dejo escrito asi, y con test, para que nadie lo quite creyendolo
  redundante.
- Vive en memoria y no se persiste: al reiniciar el mock el estado de los tokens tampoco sobrevive, de
  modo que un registro en disco solo rechazaria tokens que nadie puede volver a presentar.

### `FindIssued`: revocar un refresh token ya canjeado
- `IRefreshTokenStore` exponia `List()` (solo vivos) y una memoria de "rotados" que guardaba unica y
  exclusivamente el `family_id`. Con eso, revocar un token **ya canjeado** no encontraba nada y la
  familia quedaba viva, que es justo el camino normal de un cliente real: canjear y despues revocar el
  token anterior.
- `FindIssued(token)` busca en vivos **y** en rotados, y devuelve el `RefreshToken` completo. Por eso
  `_rotated` paso de guardar un record reduzido a guardar el token entero: hace falta el `client_id` para
  que revocar el token de otro cliente no le corte la sesion.
- Se sustituyo el record `RotatedToken` por el token entero en lugar de ampliar el record a mano: menos
  tipos y una sola fuente de verdad para cliente, sujeto, scopes y familia.

### `introspect` y `revocation` autentican al cliente
- Antes ambos leian `client_id` del cuerpo **sin comprobar nada**: `/introspect` permitia preguntar por
  el estado de cualquier token del mock y `/revocation` permitia cerrar las sesiones de otro cliente
  con solo conocer su `client_id`. Ambos exigen ahora `ClientAuthenticator` (basic o post), y la
  ausencia de credenciales es `invalid_client` 401, no un `active=false` ni un 200.
- Se extrajo `ClientCredentialsReader` para que token, introspect y revocation lean las credenciales
  igual: encabezado `Authorization` gana al cuerpo (RFC 6749 2.3.1), sin tres reglas distintas.
- Publicos siguen entrando por `client_id` solo, que es lo que ya hacia `ClientAuthenticator`.

### `WWW-Authenticate` en userinfo
- RFC 6750 3: el 401 de un recurso protegido lleva el reto Bearer. **Sin credenciales** el reto va sin
  codigo de error (`Bearer`), porque no se puede afirmar que un token sea invalido si no se presento
  ninguno; **con token invalido** va `Bearer error="invalid_token", error_description="..."`. El reto
  se compone en `BearerChallenge` (Core) porque su forma es protocolo, no host.
- `userinfo` acepta ademas el token en el cuerpo o en el query string (RFC 6750 2.2 y 2.3), no solo en
  el encabezado: hay clientes que no pueden poner cabeceras. El encabezado manda si vino.

### Bug encontrado por los tests: la clave de firma se liberaba en cada lectura
- `AccessTokenReader` hacia `using var signingKey = signingKeyProvider.GetSigningKey()`. Pero la clave
  **es del host** y `PemSigningKeyProvider` la cachea entre peticiones: la primera lectura de un token
  la liberaba y la segunda lanzaba `ObjectDisposedException`. En el proceso real eso significaba que
  `/userinfo`, `/introspect` y `/revocation` respondian 500 **desde el segundo token en adelante**; solo
  funcionaba la primera peticion de cada proceso.
- No lo detectaba ningun test porque cada test leia un solo token. Se anadio
  `LeeVariosTokensSeguidosConLaMismaClaveDelProveedor`, y se verifico revirtiendo la correccion.

## Prompt 7 — Grants refresh_token y client_credentials

### Familias de refresh token y deteccion de reutilizacion
- Cada refresh token lleva un `FamilyId`. El canje emite un token nuevo **de la misma familia**, de
  modo que la rotacion no rompe la linea de descendencia.
- `InMemoryRefreshTokenStore` recuerda los tokens ya canjeados en un diccionario aparte (`_rotated`).
  Si uno vuelve a presentarse, revoca la familia completa y devuelve `invalid_grant`: un refresh
  token robado que se reutiliza delata a que alguien copio la cadena entera, y la respuesta
  conservative es cortar esa cadena. Sin este registro, el token ya canjeado era sencillamente
  "desconocido" y el robo pasaba desapercibido.
- Los registros de `_rotated` caducan con el `ExpiresAt` del token que recuerdan, en el mismo
  `Expire()` que limpia los vivos, para que el store no crezca sin limite.
- `RevokeFamily` es parte de `IRefreshTokenStore` porque la revocacion en cascada es politica del
  almacen, no del handler: el handler solo informa de que hubo reutilizacion.

### El scope de un refresh token solo puede reducirse
- RFC 6749 6: pedir un scope que el token original no concede es `invalid_scope`. El handler
  compara los scopes solicitados contra los concedidos; reducir es valido, ampliar no.
- **Esto exigio una distincion que no existia**: `TokenEndpointService` inyecta `openid` como scope
  por defecto cuando el cliente no pide ninguno, asi que el handler recibia `["openid"]` en ambos
  casos y no podia distinguir "no me interesa el scope" de "quiero solo openid". Se aniadio
  `TokenRequest.ScopesRequested` para que la peticion sin scope conserve el concedido y la peticion
  con scope se compare. Sin ese campo, una peticion sin scope habria reducido los tokens a `openid`
  en silencio tras cada rotacion.

### client_credentials solo para clientes confidenciales
- Un cliente publico (`require_client_secret=false`) no tiene secreto con el que autenticarse, asi
  que no puede probarse su identidad en este grant: se rechaza con `invalid_client`.
- El rechazo va en el handler, no en el servicio, porque la regla es del grant. El servicio ya
  valida `allowed_grant_types`; esta es una restriccion adicional de confidencialidad.
- `sub` es el `client_id` y no se emiten `id_token` ni `refresh_token`: no hay usuario detras, y por
  eso el access token no proyecta claims de usuario aunque se pida `email` o `profile`.

## Prompt 6 — POST /connect/token, registro de grants y autenticacion de cliente

### El token endpoint es un registro de estrategias
- `IGrantHandler.HandleAsync` devuelve `Task<Result<TokenResponse>>` como pide el enunciado, aunque hoy
  ningun grant haga E/S: la firma asienta el contrato (un grant podria consultar una clave remota o
  un perfil externo) y obliga a que el endpoint sea `async` de verdad. Cada handler envuelve su cuerpo
  sincrono con `Task.FromResult`, sin `async` vacio para no provocar CS1998.
- `GrantHandlerRegistry` arma el diccionario por el **orden de registro en DI**: agregar un grant es
  una linea mas en `AddOidcMockProtocol`, sin tocar el dispatcher. Un `grant_type` sin handler es
  `unsupported_grant_type`, igual que un grant que el cliente no tiene permitido.
- La autenticacion del cliente es **otra Strategy**, no un `if` en el endpoint: `IClientAuthenticator`
  con una implementacion por metodo (`ClientSecretBasicAuthenticator`, `ClientSecretPostAuthenticator`)
  y un coordinador, `ClientAuthenticator`, que resuelve el cliente por `client_id` y delega la
  comparacion del secreto. La comparacion en tiempo constante es `ClientSecrets`, compartido por
  ambos metodos, porque es la misma regla con dos lecturas del secreto.
- `TokenEndpointRequest` lleva un `ClientCredentials` (metodo + client_id + secreto) en vez de dos
  cadenas sueltas: el endpoint no debe saber de donde salio cada valor, solo que metodo se uso.
- El discovery ya no tiene su propia lista de metodos de autenticacion: anuncia
  `ClientAuthenticator.SupportedMethods`, de modo que no puede quedar por detras de lo que el token
  endpoint acepta.

### Precedencia de las credenciales
- **El encabezado `Authorization` gana al cuerpo** (RFC 6749 2.3.1). Antes se mezclaba con
  `values["client_id"] ?? ReadBasicAuth(...)`, que ademas de ser ambiguo dejaba que un
  `client_secret` del cuerpo autenticara a un cliente que habia elegido `client_secret_basic`.
  Ahora el endpoint elige metodo: con encabezado Basic es `client_secret_basic`; sin el, es
  `client_secret_post` (que es tambien lo que hacen los clientes publicos, que solo se identifican
  por `client_id`).
- Cliente publico (`require_client_secret=false`) se acepta solo por `client_id`, y se acepta aunque
  le sobre un secreto: en `authorization_code` el secreto no viaja y la proteccion la da el PKCE.
- El base64 del encabezado vive en `BasicAuthorizationHeader`, no en el endpoint: un encabezado que
  no es Basic o un base64 corrupto no son credenciales, y la peticion acaba en `invalid_client`.

### Que tokens emite el authorization_code
- El `refresh_token` se emite **solo si el code lleva `offline_access` y el cliente lo tiene
  permitido** (OpenID Connect Core 11). Antes se emitia siempre, lo que hacia que cualquier cliente
  obtuviera un refresh token sin haberlo pedido. Se aplico tambien `offline_access` al
  `allowed_scopes` del cliente de ejemplo y al scope por defecto de las pruebas de integracion.
- El `id_token` se emite **solo si el code lleva `openid`**. El authorize endpoint ya lo exige
  siempre, asi que la comprobacion es una red de seguridad para un code emitido por otra via.
- **El code se consume SIEMPRE**, incluso cuando falla una validacion posterior (PKCE, redirect_uri,
  cliente). El canje se hace primero y las demas comprobaciones van despues, de modo que un code no
  se puede reutilizar para tantear las validaciones. Los tests lo fijan intentando un segundo canje
  con los datos correctos.

### Cabeceras del token endpoint
- Toda respuesta del token endpoint, **tambien los errores**, lleva `Cache-Control: no-store,
  no-cache` y `Pragma: no-cache` (RFC 6749 5.1). Sin esto un 401 por secreto equivocado o un token
  emitido podrian quedar cacheados por un proxy. Se pone en el `HttpContext` antes de escribir la
  respuesta, para que aplique igual al exito y al error.

### Cliente de ejemplo
- Se anadio `web-app-confidencial` (con secreto y `authorization_code`) porque para probar "un code de
  otro cliente" hace falta un segundo cliente que **si** tenga ese grant permitido: con
  `backend-service` la respuesta correcta era `unsupported_grant_type` y la prueba no llegaba a
  comprobar el `invalid_grant`. Los dos tests que afirmaban "hay 2 clientes" ahora comprueban que
  existen los clientes concretos, no el numero, para que agregar un cliente de ejemplo no los rompa.

## Prompt 5 — GET/POST /connect/authorize, cadena de validadores y sesion

### La cadena son clases, no delegados
- Cada comprobacion es un `IAuthorizeRequestValidator` con **una sola regla**, registrada en DI y
  aplicada en orden con el primer fallo. Antes eran delegados (`AuthorizationRule`) dentro del
  validador: con una regla por clase, cada una tiene su test con nombre propio y se puede probar
  sin montar la cadena entera. La lista es el **orden de registro** en DI.
- La regla declara `ErrorIsRedirectable`. **Solo `ClientExistsValidator` y `RedirectUriValidator`
  devuelven `false`**: antes de verificar `client_id` y `redirect_uri` no hay una URL de confianza,
  y redirigir ahi seria un *open redirect*. La regla de seguridad queda en la regla que la aplica,
  no en un condicional del endpoint.
- `KnownScopesValidator` (existe en `scopes.json`) se separo de `AllowedScopesValidator` (permitido
  para el cliente): son dos reglas con dos repositorios distintos, y un token con un scope que el
  mock no sabe proyectar a claims es un fallo distinto de un scope no autorizado.
- **`openid` es obligatorio**: sin el, `/connect/authorize` seria un OAuth plano y el `id_token` no
  tendria sentido. Se aplica sobre `AuthorizationRequest.EffectiveScopes`, que sustituye al
  `openid` implicito cuando la peticion no trae `scope` (compatibilidad con el comportamiento previo).
- Un `code_challenge_method` **en blanco** cuenta como ausente, igual que un `prompt` en blanco: un
  formulario reenvia vacios los campos que el cliente no relleno, y `plain` es el default de RFC 7636.
- `PromptValues` y `ResponseModes.Supported` son la unica fuente de los valores admitidos, y los
  comparten el validador y el documento de discovery.

### Sesion, prompts y pantallas
- `IAuthSessionStore` guarda `AuthSession` en memoria con expiracion por `TimeProvider`; la cookie
  es propia del mock (`oidc_mock_session`, `HttpOnly`, `SameSite=Lax`, `Path` = `PathBase`) y no la
  de ASP.NET, porque el unico estado que hace falta es "este navegador ya se autentico como
  alguien" y una cookie firmada obligaria a configurar autenticacion completa para un mock.
  `Close()` y no `End()`: **CA1716** prohibe `End` como miembro de interfaz (palabra reservada en VB).
- `AuthorizationInteraction.Decide` es un caso de uso aparte del endpoint: decide `Login`,
  `Consent`, `Grant` o `Error` a partir del prompt y de la sesion. El endpoint solo traduce la
  decision a una pantalla o a una redireccion.
  - `prompt=none` **prohibe cualquier pantalla**: sin sesion devuelve `login_required` y con sesion
    concede en silencio. `prompt=login` se comprueba **antes** de mirar si hay sesion, porque su
    efecto es precisamente olvidarse de ella. `prompt=consent` con sesion muestra la pantalla de
    consentimiento; sin sesion cae en login, porque consentirse no autentica a nadie.
  - El POST de consentimiento **exige sesion vigente**: si caduco, `login_required` en vez de
    conceder. La pantalla de consentimiento no es un factor de autenticacion.
- `AuthorizationService.Approve` emite el codigo a nombre de un usuario ya autenticado y `SignIn`
  acaba delegando en el, de modo que el codigo se emite **en un solo sitio** tanto si el usuario
  tecleo contrasena como si venia de la sesion.
- `AuthorizationFlow` (Host) encapsula las dependencias de una peticion para que los metodos del
  endpoint no lleven ocho parametros; `AuthorizationEndpoints` solo mapea las rutas.

### Login y consentimiento
- HTML **sin JavaScript**, como se pidio. El campo de usuario es un `<input list=...>` con
  `<datalist>` alimentado de `users.json`: ofrece el desplegable y admite escribir, en un solo
  campo. El endpoint acepta `user` (desplegable) y `username` (texto) y **prefiere el tecleado**.
- Los errores que viajan por el `redirect_uri` **respetan el `response_mode`**, tambien en
  `form_post`. Antes se forzaban por query: el cliente que pidió `form_post` recibia un 302 con el
  error, que es justo lo que `form_post` existe para evitar.
- El branding del cliente (display name, logo y color) se renderiza en las dos pantallas. Los tests
  comparan el display name **codificado con `HtmlEncoder`**, porque el HTML que produce el mock lo
  escapa y comparar el texto en crudo pasaria por un bug de renderizado.

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

## Etapa 9 — /connect/endsession con cierre de sesión y frontchannel logout

### Decisiones

- **`EndSessionService` (Core) es el caso de uso, no el endpoint**. El orden es deliberado: resolver
  cliente → validar `post_logout_redirect_uri` → **después** cerrar sesión. Validar primero evita que una
  petición inválida (un hint manipulado, un redirect ajeno) tire la sesión de un navegador que solo
  está probando una URL. La cookie, en cambio, se borra siempre en el endpoint: es lo que el navegador
  necesita para no reenviar una sesión que el mock ya rechazó cerrar.
- **El `id_token_hint` manda sobre el `client_id`**: es el único de los dos que va firmado, así que su
  `aud` es el cliente contra el que se valida el redirect. Si vienen los dos y no coinciden, es
  `invalid_request` en vez de dar prioridad en silencio: si no, un `client_id` de otro cliente bastaria
  para intentar redirigir fuera de su propia lista.
- **`id_token_hint` inválido es `invalid_request` 400**, no se ignora como "opcional". Si se aceptara,
  un hint manipulado sería indistinguible de uno ausente y el redirect quedaría validado contra otro
  cliente. El servidor real puede ignorar hints caducados; el mock prioriza no abrir la redirección.
- **Sin `client_id` ni hint, el `post_logout_redirect_uri` se busca en la lista de todos los clientes**.
  Antes se hacía así y algunos clientes reales no mandan `client_id`. Sigue impidiendo el open redirect:
  si ningún cliente lo tiene registrado, se rechaza.
- **El `sid` del aviso de frontchannel es el de la sesión que se acaba de cerrar**, no un claim `sid`
  del id_token: emitirlo exigiría propagar el identificador de sesión desde el authorize hasta el
  token endpoint (code → grant → `IdTokenRequest`), que es otro cambio de alcance propio. Con la
  decisión actual el aviso lleva el `sid` real de la sesión cerrada, y vacío si no había cookie.
- **`SignedTokenValidator` extraído de `AccessTokenReader`**: access token e id token se validan igual
  (firma, issuer, vigencia contra `TimeProvider`) y solo cambia la política de audiencia. Tenerlo en dos
  sitios era copiar la parte que más bugs dio (la clave del host liberada con `using`).
- **`Client.FrontchannelLogoutUri` es opcional** (`= null`) para no romper los constructores
  posicionales existentes, y el discovery ahora anuncia `frontchannel_logout_supported: true`, que es lo
  que hace el servidor real.

### Verificación

Revertir `sessions.Close` (por `Expire`) = 1 fallo unitario; revertir el iframe de la página de cierre =
1 fallo de integración. El test de cierre real de sesión se apoya en el flujo completo
(`SignInAsync` → authorize `prompt=none` con code → `endsession` → `prompt=none` con `login_required`).

## Etapa 10 — temas transversales: errores, CORS, logging, opciones, HTTPS y publicación

### Decisiones

- **`ProblemDetails` solo para lo no previsto.** `UnexpectedErrorHandler` (un `IExceptionHandler`) traduce
  cualquier excepción que nadie capturó a `application/problem+json` con `status`, `title`, `instance` y
  `traceId`. Los errores de protocolo **no pasan por ahí**: los endpoints los forman con el cuerpo OAuth de
  RFC 6749 5.2 (`error` + `error_description`, `application/json`). Un cliente que espera `invalid_grant`
  no puede encontrarlo dentro de un `ProblemDetails`, y al revés: un 500 del mock no debe parecer un
  rechazo del servidor real. `WriteAsJsonAsync` sobrescribe el `Content-Type`, así que el media type
  `application/problem+json` se pasa como argumento explícito en vez de fijar la cabecera a mano.
- **El detalle de la excepción nunca sale al cliente**, solo al log. La respuesta lleva el `traceId`, que
  es lo que permite correlacionar el fallo del servidor con el del cliente.
- **CORS con `ICorsPolicyProvider` propio, no con `AddPolicy` de `AddCors`.** Los orígenes salen de
  `OidcMockOptions`, que solo existe en la DI ya construida; con `AddPolicy` habría que fijarlos al
  registrar, antes de que la configuración esté enlazada y validada. El provider construye la política
  por petición desde `IOptions`, así que manda la configuración.
- **La política se aplica endpoint a endpoint (`RequireCors`), no con `UseCors` global.** El discovery,
  el JWKS, el token y el userinfo los consume un navegador desde otra página; el authorize con su
  formulario, el introspect y el revocation son de backends y quedan fuera. Un `UseCors` global expondría
  de más.
- **`AllowCredentials` sin comodín.** El token endpoint admite credenciales de cliente, y
  `Access-Control-Allow-Origin: *` con credenciales es inválido en los navegadores. El validador rechaza
  el `*` explícitamente y obliga a enumerar orígenes. Por defecto: `localhost:4200`, `5173` y `3000`.
- **`AllowedCorsOrigins` nace vacía y los valores por defecto se aplican en un `PostConfigure`.** El
  enlace de configuración **añade** elementos a una lista ya poblada en vez de reemplazarla, así que
  dejar el valor por defecto en el inicializador hacía que un origen configurado acabara en el índice 3 y
  los tres por defecto siguieran activos. Con la lista vacía + `PostConfigure`, configurar sustituye de
  verdad y no configurar conserva los valores por defecto.
- **El log no contiene tokens ni secretos, nunca.** `OidcMockLog` (source generator de `LoggerMessage`)
  emite un evento por operación con `EventId` y nivel: se registra el grant, el `client_id`, la caducidad y
  si se emitió refresh token; no el token, el código, el refresh token ni el `client_secret`. Un log de
  desarrollo acaba en consolas, en el pipe de CI y en archivos compartidos, y un token completo ahí es una
  credencial viva. Los tests lo comprueban contra los valores reales emitidos.
- **Los tests de log ignoran las entradas del framework.** ASP.NET registra el `RedirectResult` del
  authorize con la URL completa, que lleva el código dentro. Es comportamiento suyo y no del mock, así
  que la aserción se limita a los eventos propios; silenciarlo sería mentir sobre lo que hace el host.
- **`OidcMockOptionsValidator` vive en Core y es estática**, sin referencia a ASP.NET, para poder
  probarse como dominio puro; el adaptador `IValidateOptions` en el Host la conecta con `ValidateOnStart`.
  Devuelve **todos** los problemas, no el primero: un arranque con la configuración rota dice todo lo que
  está mal de una vez.
- **`ServingOptions` es una subsección (`OidcMock:Serving`)**, y eso importa: leer la sección `OidcMock`
  entera devolvía los valores por defecto, así que el contenedor acababa sirviendo HTTPS con el
  certificado de desarrollo en lugar de HTTP plano. El bug se detectó ejecutando el binario publicado, no
  en los tests, y quedó cubierto por un test de binding.
- **Sin certificado de desarrollo en ruta absoluta.** `UseHttps()` sin argumentos deja que Kestrel
  resuelva el certificado de `dotnet dev-certs https`; apuntar a un PFX en `~/.dotnet/corefx` a mano
  depende de una ruta interna del SDK.
- **El mock sigue firmando aunque no pueda persistir la clave.** Un `config` montado en solo lectura
  (Docker con usuario no-root) es un caso normal, no un fallo: antes el JWKS devolvía 500 con
  `UnauthorizedAccessException`. Ahora genera la clave en memoria y avisa por log de que el `kid` cambiará
  en cada reinicio. Persistir el `kid` es una comodidad; servir, no.
- **Publicación autocontenida y de un solo archivo solo en el proyecto Host, y solo si hay RID.** Poner
  `SelfContained` en `Directory.Build.props` arrastraba a los proyectos de test, que no lo quieren.
  `scripts/publish.sh` publica `linux-x64` y `win-x64`; el `Dockerfile` es multi-stage con el SDK para
  compilar y `aspnet` para ejecutar.
- **Los JSON de config se copian a `/app/config` en el contenedor, aparte.** Con single-file acaban en el
  directorio de extracción (`~/.net/...`), que no es un sitio donde buscar ni sobreescribir
  configuración. Con `OidcMock__ConfigDirectory=/app/config` el config se puede montar encima con `-v`.

### Bugs reales encontrados y corregidos de paso

- **La clave de firma se liberaba en el endpoint del JWKS.** `using var signingKey = ...GetSigningKey()`
  liberaba la instancia **cacheada** del proveedor: todo token emitido después fallaba con
  `ObjectDisposedException`. La clave la posee y libera el proveedor, no el endpoint. Nadie lo notaba
  porque el fallo solo salía si un test pedía el JWKS y luego firmaba.
- **La cache de firma de IdentityModel cruzaba hosts.** `CryptoProviderFactory` cachea el proveedor de
  firma por `kid`, así que dos instancias del mock con la misma clave (dos `WebApplicationFactory`) se
  pisaban la RSA: al liberar la primera, la segunda fallaba al firmar. Se desactivó la cache
  (`CacheSignatureProviders = false`) con una factory propia. El caso se reprodujo aislado, fuera del
  repositorio, antes de tocar código.

### Verificación

`dotnet build` en verde (0 warnings) y **507/507 tests** (330 unit + 177 integration), con la suite de
integración repetida varias veces porque los dos bugs de clave se manifestaban de forma intermitente. La
imagen se construyó y se levantó de verdad: discovery y JWKS 200, `client_credentials` emitiendo tokens,
CORS presente y el aviso de clave no persistida apareciendo en el log.

## Comandos

- `dotnet build` en verde (0 warnings) y **507/507 tests** (330 unit + 177 integration).
- `scripts/publish.sh` genera los ejecutables autocontenidos de `linux-x64` y `win-x64`;
  `docker build .` produce una imagen que sirve por HTTP plano en el puerto 8080.
- Commits: `test:` tests primero, `feat:` implementación, `fix:` bugs hallados al verificar,
  `docs:` estas decisiones.

### Formato de los archivos
- Raíz con clave: `{"clients": [...]}`, `{"users": [...]}`, `{"scopes": [...]}` (permite agregar
  metadatos futuros sin romper el formato).
- Las vigencias se expresan como `TimeSpan` en formato `hh:mm:ss` (`"access_token": "00:30:00"`).
- Nombres de campos en `snake_case` (`client_id`, `redirect_uris`, `token_lifetimes`), que es lo habitual
  en archivos de configuración a mano.
- `client_secret` en texto plano, tal como se pidió (los secretos de `users.json` también, ya que el
  mock es solo para desarrollo local y offline).

## Etapa 11: README, revision critica y bug del refresh

### El canje de refresh_token tiene que devolver id_token

- **Bug real, encontrado probando contra un cliente real.** `RefreshTokenGrantHandler.IssuesIdToken`
  valia `false` y habia un test que lo fijaba como "decision del mock". OpenID Connect Core 3.1.3.3
  dice lo contrario: si el refresh conserva el scope `openid`, la respuesta trae un `id_token` nuevo.
  Sin el, una aplicacion que renueva la sesion no puede volver a validar al usuario, y el handler
  `Microsoft.AspNetCore.Authentication.OpenIdConnect` da por hecho que llega.
- **Como se detecto:** el canje por HTTP devolvia `access_token`, `refresh_token` y `scope`, pero no
  `id_token`. El sintoma se veia como un token renewal que "no funcionaba", no como un token mal formado.
- **Arreglo:** `IssuesIdToken => true` mas la regla compartida `IdTokenRules.GrantsIdToken(scopes)`, que
  antes vivia duplicada como metodo privado en el grant de codigo. Sin `openid` no se emite: un canje
  OAuth a secas no inventa identidades. El `id_token` renovado no lleva `nonce`, porque no es una
  peticion de autorizacion.
- **Decision:** el comportamiento anterior era una interpretacion defendible del RFC 6749 en isolation,
  pero incompatible con OpenID Connect y, sobre todo, con clientes reales. Mandó la prueba de
  compatibilidad sobre el criterio previo.

### Defectos del arnés de pruebas (no del mock)

- **La sesion se perdia entre pasos.** El helper hacia el login en un `BrowserSession` y lo descartaba;
  las pruebas siguientes abrian otro sin la cookie, asi que se probaba el refresh contra un cliente sin
  sesion. Ahora el mundo firmado y el navegador se devuelven juntos en un `SignedInWorld`.
- **`Results.SignOut` con un solo esquema** abandona la sesion del proveedor pero deja viva la cookie de
  la aplicacion: el logout parecia funcionar y la sesion seguia abierta. Se cierran los dos esquemas.
- **Un test afirmaba algo falso:** el segundo `/refresh` usa el token que la renovacion acaba de canjear,
  asi que era la renovacion correcta la que fallaba. Para observar la rotacion hace falta reenviar el
  token ya canjeado, y para eso `/refresh?anterior=1` lo reenvia a proposito.
- **La API de JwtBearer no exigia autorizacion:** sin `.RequireAuthorization()` respondia 200 con un
  principal vacio, y los casos negativos no podian fallar.
- `ProtocolMessage.Scope` vacio en `OnTokenValidated` era una lectura equivocada, no un bug: en el camino
  de userinfo ese mensaje no es la respuesta del token endpoint. Los diagnosticos temporales se
  retiraron al confirmar la causa real.

### Verificacion

`dotnet build` en verde (0 warnings) y **528/528 tests** (332 unit + 178 integration + 18 compatibilidad
de cliente). La suite de compatibilidad levanta el mock sobre Kestrel real y usa los clientes de verdad:
`OpenIdConnect` para el login, la renovacion y el logout, y `JwtBearer` para la API, ambos configurados
solo con el issuer.

### Revision critica: que se aplico y que no

De la revision externa (ALTO/MEDIO/BAJO) se aplicaron los ALTO que se podian verificar con tests:

- **El canje de refresh ahora devuelve id_token** (bug real, ver arriba).
- **`AuthorizationBinder` nuevo.** `AuthorizationFlow` hacia bind -> validate -> seguir en los dos
  caminos, GET y POST, con unas 25 lineas duplicadas. Ahora el enlace y la validacion viven en un tipo
  con su propio nombre, y `AuthorizationFlow` solo decide la pantalla. El metodo se llama `Bind` y no
  `BindAsync` porque Minimal API busca por convencion un `BindAsync` en los tipos que inyecta y lo toma
  por un binder de parametros: con el otro nombre, todos los endpoints devolvian 500.
- **Los errores de validacion se devuelven tal cual los produce el validador.** Al extraer el binder se
  perdia su decision de si un error puede redirigirse al `redirect_uri` del cliente, y el authorize
  paso a responder 400 donde antes redirigia. El validador decide eso para no convertir el authorize en
  un vector de open redirect, asi que el `AuthorizationValidationResult` travels entero, no solo su
  error.
- **`CreateStore<TStore>` en la composicion.** Los tres factories de los stores JSON eran copias
  literales; ahora hay un metodo generico.
- **Tests de logging que no pueden pasar en vacio.** Comprobaban que el log no menciona el token o el
  secreto, pero una ausencia tambien se cumple con un log vacio: si el provider no recogia nada,
  pasaban sin comprobar nada. Ahora un guard (`AssertLoggedSomething`) exige que el mock haya registrado
  algo, y hay una asercion positiva de que el log dice el cliente y el grant.
- **Tests del propio arnes de compatibilidad.** `BrowserSession`, `OidcClientHost` y `TokenRefresher`
  reimplementan un cliente OIDC a mano, unas 600 lineas sin verificar. Si esa pieza falla, los tests de
  login, refresh y logout pasan o fallan por el motivo equivocado. Ahora hay tests propios del arnes:
  que el login deja sesion, que el renovador emite tokens y deja la sesion abierta, que guarda el token
  canjeado y que no inventa una sesion que no hay.
- **Los fallos intermitentes de la suite de integracion eran reales y no del sistema.** Dos causas
  encadenadas, ambas de infraestructura de pruebas: `OidcTestClient.Create` devolvia el `HttpClient` de
  `CreateClient` sin propagar el `Dispose` al factory, asi que cada prueba filtraba un host con sus
  watchers; y `WebApplicationFactory` montaba un `PhysicalFileProvider` con watcher para el content root.
  Con la suite creciendo se agotaba el limite de inotify del sistema y los tests fallaban con errores de
  infraestructura que no tenian nada que ver con lo que comprobaban. Arreglado con un cliente que libera
  tambien el host, fijando el content root, desactivando la recarga en caliente en las pruebas, un
  fixture de ensamblado que recoge los hosts no liberados y sin paralelismo. Verificado en tres vueltas
  seguidas.
- **`docs/decisions.md`** (este fichero) y el `README.md` nuevo.

**No se aplicaron** los ALTO de refactor estructural grande (partir `TokenEndpoints` en cinco clases,
mover la traduccion del token request a Core) y varios MEDIO: son cambios de organizacion que tocan
mucho codigo sin corregir un defecto observable, y el criterio de este prompt es priorizar lo que un
cliente real o una prueba puede detectar. Quedan anotados aqui y en el informe de revision.

### Coherencia de la configuracion de ejemplo

Dos defaults que rompian a un desarrollador, encontrados al revisar el discovery contra los datos:

- `backend-service` declaraba `centralenlinea.bccr.fi.cr`, un scope que no existia en `scopes.json`:
  cualquier peticion de token de ese cliente devolvia `invalid_scope`.
- Los claims `Bccr.IdEntidad`, `Bccr.IdUsuario`, `Bccr.CodTipoId` y `Bccr.negocio` estaban en
  `users.json` pero en ningun scope, asi que el proyector nunca los emitia. Tambien `phone_number`, sin
  scope `phone` que lo proyectara.

Cubierto con `ConfigCoherenceTests`, que falla si un cliente pide un scope inexistente o si un usuario
tiene un claim que ningun scope puede emitir. Los dos casos son invisibles al arrancar el mock: el
fallo aparece en la primera peticion que alguien hace.

Ademas, el discovery anunciaba `backchannel_user_code_parameter_supported: true` y la respuesta de CIBA
no traia `user_code` ni `verification_uri`. Ahora los devuelve cuando el cliente declara que los admite
con `user_code_parameter_supported`, y no si no.

## Etapa 12 (2026-10-02): segunda revision critica y cobertura

### Revision critica aplicada (solo los ALTO)

Una revision externa sobre el codigo de la etapa 11 dejo 20 hallazgos. Se aplicaron los de prioridad
alta, cada uno con su test primero. **541 tests en verde** (335 unit + 184 integration + 22
compatibilidad).

- **Device authorization y CIBA no autenticaban al cliente.**：`PollEndpoints` hacia
  `clientStore.Find(client_id)` directo del cuerpo, sin `ClientAuthenticator`, mientras que PAR, token,
  introspect y revocation si lo hacian. Un cliente confidencial podia pedir un device code con un
  secreto equivocado: es la via por la que un atacante pide una autorizacion en nombre de otro cliente.
  Ahora `IPollAuthorizationService` recibe `ClientCredentials` y autentica con el mismo coordinador que
  el token endpoint. Tests: `DeviceAuthorizationRechazaSecretoIncorrectoConInvalidClient` y su gemelo de
  CIBA (fallaban antes del arreglo).
- **La caducidad de CIBA la fijaba `DateTimeOffset.UtcNow` dentro del Host**, contra la regla de que
  todo el reloj pasa por `TimeProvider` inyectado. Ningun test podia comprobar la expiracion de una
  peticion CIBA porque no habia forma de mover el reloj. `PollAuthorizationService` lo recibe inyectado.
  **La logica de negocio de CIBA (handle, caducidad, peticion validada, aprobacion por `login_hint`)
  estaba en el endpoint**, duplicando lo que hacia `DeviceAuthorizationService`. Los dos viven ahora en
  el mismo servicio de Core: son familia de verdad, comparten store, reloj y ciclo de sondeo.
- **`PendingAuthorizationRequest.Issuer` era un campo muerto**: lo escribian tres sitios y no lo leia
  nadie (el grant usa el issuer del token endpoint). Ademas era la via por la que PAR guardaba
  `options.Issuer ?? string.Empty`, o sea **el issuer vacio** cuando este se deduce del host, con una
  segunda regla de escritura distinta de la de CIBA. Un campo que se escribe de dos maneras y no se lee
  no puede quedarse: se borro, y `Push` dejo de recibir el issuer.
- **`TokenTestValidator.Validate` desactivaba la vigencia en silencio** si no se pasaba reloj
  (`ValidateLifetime = timeProvider is not null`). Seis asserts pasaban con un token caducado hace años.
  Ahora el reloj es obligatorio: al hacerlo, el compilador senalo los diez call sites afectados, que
  era exactamente la lista de comprobaciones debiles.
- **La suite de compatibilidad compartia el log global sin desactivar el paralelismo**, y `Clear()`
  no lo llamaba nadie: un volcado diagnostico mezclaba el log de todos los tests vivos. Ahora el
  ensamblado no paraleliza y `CompatibilityWorld.StartAsync` limpia el almacen, que es el unico punto
  por el que pasan todos los tests.
- **`AuthorizationService.Deny` no denegaba nada**: devolvia un `Fail` fijo y no tocaba ningun store.
  Solo lo usaba un test. Se borro en vez de renombrarlo: un nombre que promete un efecto que no tiene
  hace que quien lo lea asuma estado que cambia.
- **`ClientCredentialsReader` vivia dentro de `BearerChallengeResults`**, y el token endpoint tenia su
  propia copia de la lectura de credenciales. Tres sitios decidiendo como se presenta un cliente.
- **Los mensajes de error de los flujos por sondeo decian "codigo de autorizacion"**, donde lo que el
  cliente presenta es un `device_code` o un `auth_req_id`. El mensaje es superficie observable del
  mock y el cliente lo muestra.
- **`SplitScopes` estaba copiado cuatro veces** con las cuatro decidiendo por su cuenta. Ahora es
  `ScopeNames.Split`.

### Lo que NO se aplico, y por que

Los MEDIO y BAJO restantes (partir `TokenEndpoints` en cinco clases, partir las clases de DI,
`TokenRequest` con banderas posicionales, `PollGrantHandler.HandleFrom` identico en ambas subclases,
`"OidcMock"` duplicado en el validador de opciones) son reorganizacion o estilo: tocan mucho codigo sin
corregir un defecto observable. Se dejan anotados para cuando se toquen esos archivos.

### Cobertura: no se pudo medir de verdad

No hay herramienta de cobertura en el repositorio y anadirla incumpliria la regla de dependencias
minimas sin autorizacion. Se probo `coverlet.collector` y `Microsoft.Testing.Extensions.CodeCoverage`
**de forma temporal y revertida**, sin tocar el grafo de proyectos: `coverlet.collector` no lo suporta
el runner in-process de xunit v3, y la extension de cobertura de Microsoft.Testing.Platform choca por
version con `Microsoft.Testing.Platform` 2.4. Medirlo exige anadir un paquete de test, que es una
decision del proyecto, no una medicion. La cifra por proyecto que se reporta es **estimada por analisis
estatico** (que tipos/nombres aparecen en las pruebas), no instrumentada.

## Etapa 13 (2026-10-02): cerrar los huecos de pruebas de Core

Solo pruebas: **no se anadio funcionalidad ni dependencias**. **611 tests** (405 unit + 184 integration +
22 compatibilidad).

### El bug que aparecio al escribir las pruebas

Al probar `TokenResponseFactory` se vio que **dos de los cuatro grants que emiten tokens no aplicaban
la regla del `openid`**:

- `PasswordGrantHandler` y `PollGrantHandler` pasaban `includeIdToken: IssuesIdToken` sin mirar los
  scopes, mientras que `AuthorizationCodeGrantHandler` y `RefreshTokenGrantHandler` si hacian
  `&& IdTokenRules.GrantsIdToken(...)`.

Es alcanzable de verdad: el token endpoint acepta `scope=email` (el cliente lo permite y el scope existe),
y el canje devolvia un `id_token` en un flujo que nunca pidio `openid` — es decir, **emitia una identidad
sin que nadie la pidiera**. Es exactamente lo que OpenID Connect Core 3.1.3.6 prohibe.

Por que no lo habia cazado nadie: `IdTokenRules` no tenia pruebas propias, y un grant que olvida
llamarla no se parece en nada a uno que la llama mal. **La regla estaba escrita y simplemente dos
invocaciones no la aplicaban.**

Arreglo: los dos grants consultan `IdTokenRules`, como los otros dos. Los dos tests fallan antes del
cambio (devolvian un token firmado) y pasan despues.

**Leccion que deja esto:** la regla no estaba mal, estaba duplicada por el codigo de cada llamador. Un
`TokenResponseFactory` que decidiera el `id_token` por si mismo, en vez de delegar, habria hecho imposible
este forget.

## `prompt=select_account`: se anuncia a proposito y sin implementar

> **Actualizado (2026-10-07): implementado.** La pantalla de identidad (sin contrasena) resulto ser la
> pantalla de eleccion de cuenta que faltaba: los perfiles de `users.json` son las cuentas. Hoy
> `prompt=select_account` muestra esa pantalla con y sin sesion, precarga la cuenta de la sesion (el
> campo `perfil` casa tambien por `subject`) y concede con el perfil elegido (`AuthorizationStep.SelectAccount`).
> Lo de abajo es el razonamiento original de por que se dejo sin implementar en su momento.

**Decision: se deja como esta. No es un olvido.**

`DiscoveryDocumentBuilder` anuncia `select_account` en `prompt_values_supported`, igual que la referencia
del BCCR, y `ResponseModes.SelectAccount` define el valor. Pero ningun camino del authorize lo trata de
forma especial: el flujo es el mismo que sin el.

**Por que se anuncia igual.**

1. **El discovery tiene que coincidir con la referencia.** El objetivo del mock es que una app apunte a
   `https://oauth2.bccr.fi.cr/personafisica/` o al mock sin cambios de codigo. Una app que valida el
   discovery contra el de la referencia rechazaria el mock si le falta un valor. Annunciar de menos rompe el
   proposito del proyecto; anunciar de mas no, porque el valor es opcional en la practica.
2. **El valor se acepta, no se rechaza.** Un cliente que envie `prompt=select_account` no recibe un error:
   completa el flujo como si no lo hubiera pedido. Un mock que devolviera `invalid_request` seria peor.

**Por que no se implementa ahora.** Es funcionalidad nueva, y el alcance de esta etapa era cerrar huecos de
pruebas sin anadir comportamiento OIDC. Ademas, `select_account` solo tiene sentido con varios usuarios en
el mismo `sub` en el store, que el modelo actual de `users.json` no representa (un usuario por
`client_id`). Anadirlo bien es un cambio de modelo, no un `if`.

**Lo que si se hizo:** el README lo declara como **"anunciado, no implementado"**, para que nadie depure
creyendo que el cambio de cuenta deberia funcionar. Esa tabla es la mitigacion: el comportamiento observable
es el de la referencia y la limitacion esta escrita.

Si algun dia se implementa, el orden seria: permitir varios usuarios por `client_id` en el store, y luego
un selector en la pantalla de autorizacion que fije el `sub` de la sesion.

## Etapa 14 (2026-10-05): deuda tecnica, cuatro bugs reales

Revision de la deuda tecnica con dos agentes de analisis y verificacion manual de cada hallazgo antes de
tocar nada. Todo lo de aqui son **bugs de protocolo o de seguridad**, no estilo. **637 tests** (419 unit +
196 integration + 22 compat.).

### D-037 — La cookie de sesion se borraba con un `path` distinto al de escritura

**Bug.** `AuthSessionCookie.Write` emitia la cookie con `Path = /personafisica`; `Clear` la borraba con
`response.Cookies.Delete(Name)`, que sale **sin atributo `path`**. El navegador empareja una cookie por
(nombre, dominio, path), asi que el `Set-Cookie` de borrado no emparejaba con la cookie escrita: la sesion se
cerraba en el servidor pero la credencial seguia en el navegador, en contra de lo que documenta
`EndSessionEndpoints`.

**Decision.** `Write` y `Clear` comparten una unica factoria de `CookieOptions`. No basta con "pasar el path
bien": la pareja nombre+path es lo que empareja el borrado, y tocarlas por separado volveria a romperlo.

**El test anterior pasaba en vacio.** `Assert.Contains("expires=")` se cumple tanto si el borrado acierta como
si no. Ese es el detalle que dejo el bug vivo: un assert de cabecera tiene que comprobar el valor que
importa, no la presencia de una palabra.

### D-038 — El sondeo no implementaba `expired_token` ni `slow_down`

**Bug.** `ProtocolErrors.ExpiredToken` y `SlowDown` estaban declarados y **no los usaba nadie**. Un
`device_code` caducado respondia `invalid_grant`, que al cliente le dice "este handle no vale" cuando lo que
paso es "este handle caduco, pide otro". Y el `interval` que anuncian `deviceauthorization` y CIBA no se
cumplia.

**Decision.** `IPendingAuthorizationStore` gana `Poll`, que separa los tres finales que `Find` mezclaba:
desconocido (`invalid_grant`), caducado (`expired_token`) y vivo (el resto del ciclo). `Find` no puede
servir para esto porque al encontrar un caducado lo descarta en el momento, y sin ese rastro el grant no
tiene forma de nombrarlo.

**El primer sondeo nunca es `slow_down`.** El cliente no puede respetar un intervalo que aun no conoce, asi
que frenarlo en el primer intento lo deja sin poder avanzar. El intervalo es por handle: frena a quien sondea
de mas, no a los demas flujos.

### D-039 — PAR autentica al cliente con una comparacion propia

**Bug.** `PushedAuthorizationService` leia `client_id` y `client_secret` **solo del cuerpo** y comparaba con
`string.Equals`. Dos desviaciones: era la **unica** comparacion de secreto del repositorio que no pasaba por
`ClientSecrets` (sin tiempo constante), y no aceptaba `client_secret_basic`, que el propio discovery anuncia:
un cliente real que manda el secreto en el encabezado `Authorization` recibia `invalid_client`.

**Decision.** `PushRequestParameters` lleva `ClientCredentials` ya resuelto; el endpoint lo lee con el mismo
`ClientCredentialsReader` que el token endpoint (con la precedencia del encabezado de RFC 6749 2.3.1) y el
servicio se autentica con el `ClientAuthenticator` de la DI. Se borra la cuarta copia de autenticacion de
cliente del host.

**Por que en el caso de uso y no en el endpoint.** El endpoint lee HTTP; el caso de uso decide. Duplicar la
autenticacion en PAR es lo que produjo el bug, y la unica forma de que no vuelva es que no haya dos formas.

### D-040 — `no-store` en un solo endpoint, y a mano

**Bug.** `Cache-Control: no-store` lo ponia un unico metodo privado de `TokenEndpoints`. Lo demas devolvian
material igual de sensible sin el: el `device_code` y el `user_code`, el `request_uri` de un solo uso, el
estado de un token de terceros en introspection y los claims de una persona en userinfo.

**Decision.** Filtro `NoStoreFilter` declarado al mapear el endpoint (`.DoNotStore()`), en vez de escribir la
cabecera a mano en cada handler. El motivo no es solo DRY: escrito a mano, un endpoint nuevo **puede
olvidarse**; declarado al mapear, hay que escribir la decision al anadirlo.

**La lista de endpoints vive en el test** (`TheoryData`). Anadir uno obliga a decidir si lleva la cabecera,
que es justo la pregunta que hay que hacer al anadir un endpoint.

### Refactors de la misma etapa (sin cambio de comportamiento)

Todo verificado con `grep` sobre `src` **y** `tests` antes de borrar:

| Que | Por que se va |
| --- | --- |
| `PendingAuthorizationRequest.DeviceCode`, `.UserCode`, `.BindingMessage` | Se escribian y nadie los leia: el sondeo usa `Handle`. `BindingMessage` no tenia ni una referencia. |
| `TokenResponseFactory.Issue(timeProvider, …)` | Solo aparecia en un `ThrowIfNull`. Al quitarlo, CS9113 senalo el mismo parametro muerto en `AuthorizationCodeGrantHandler`. |
| `AuthorizationRule` (delegate) | Sin un solo uso. El fichero pasa a `AuthorizeRequestValidators.cs`, que es lo que declara. |
| `GrantHandlerRegistry.Supports`, `User.HasClaim` | Sin llamadores. |
| Listas literales del `DiscoveryDocumentBuilder` | Repetian como strings los grants, response types, modos, prompts y metodos de PKCE que ya viven como constantes. Un grant nuevo no se anunciaba y nada fallaba. |

**Leccion sobre el compilador como detector de deuda:** con `TreatWarningsAsErrors`, quitar un parametro
muerto **delata los demas**. CS9113 aparecio solo tras el primer borrado.

### Lo que se decide no hacer

- **Partir `TokenEndpoints`** (MEDIO de la etapa 12): sigue sin hacerse. Es correcto, pero es estructura sin
  bug detras, y este trabajo se centro en lo que si cambia comportamiento observable.
- **Flags posicionales de `TokenRequest`** (MEDIO de la etapa 12): sigue abierto. Ver D-024: `ScopesRequested`
  protege un fallo silencioso real, asi que arreglarlo requiere un value object, no un cambio de firma.
- **`prompt=select_account`**: fuera de alcance por decision previa (ver la seccion anterior).
- **Paralelismo de las suites**: se mantiene el serializado. Con 637 tests y puertos Kestrel reservados por
  test, paralelizar exige rehacer el arnes; el beneficio son ~10 s.

### D-041 — Un comando para la regla 8, y build reproducible

La regla 8 (build y test en verde) se cumplia con dos comandos a mano por proyecto. `scripts/test.sh` la
cierra en uno y compila en **Release**, que es donde se publica y donde los analizadores se portan distinto
de Debug.

Dos cosas mas que salen de lo mismo:

- **`global.json` fija el SDK.** Sin el flotaba con el instalado, y dos compilaciones del mismo commit podian
  dar binarios distintos. `rollForward: latestFeature` deja avanzar de version sin romperse.
- **`ContinuousIntegrationBuild` y `PathMap`** en el publish y el Dockerfile: sin ellos el ensamblado lleva
  la ruta absoluta de la maquina que lo compilo. En `Directory.Build.props` se activan **solo con
  `CI=true`**, para no ralentizar el build local.

**D-011 queda obsoleto.** Decía que `dotnet test` a nivel de solucion abortaba en este entorno
(`Internal CLR error 0x80131506`). Con .NET 10 y el runner de Testing Platform **funciona** (405 + 184 + 22
en la comprobacion de esta etapa). Por eso `scripts/test.sh` invoca las tres suites por proyecto de forma
explicita: no por el fallo, sino porque asi el script dice que corredor ejecuta cada suite.

**Suprimido a proposito:** el paralelismo entre suites y el paquete de cobertura instrumentada siguen fuera
(razones en la seccion anterior y en `progress.md` de la etapa 12).

## Etapa 15: cierre de brechas

### D-042 — Anunciar no es aceptar: dos listas de response_type

**El bug.** `ResponseTypeValidator` validaba contra `ResponseTypeNames.SupportedCombinations`, que es
la lista que el discovery **anuncia** por paridad con el BCCR, y no contra la que el endpoint sabe
**responder**. Como el authorize solo emite codigo de autorizacion, `response_type=id_token token` pasaba
la validacion, el usuario hacia login, y la respuesta era `code=...` en el fragmento: el cliente pedia
tokens y recibia un codigo que no sabe usar, **sin ningun error**. Es peor que rechazar, porque nada
le dice al cliente que fallo.

**Por que nadie lo vio.** La regla no tenia test propio: `ResponseTypeValidator` solo aparecia dentro de
dos cadenas de validacion, y ninguna pedia una combinacion implicita. Un test de paridad con el
discovery habria dado el visto bueno, porque las dos listas coincidian. El propio
`DiscoveryDocumentBuilderTests.LosValoresAnunciadosVienenDeLasConstantesDelDominio` afirmaba esa
coincidencia como si fuera una propiedad deseable, y era justamente el bug.

**La correccion.** Dos listas con dos nombres que dicen para que son, en `ResponseTypeNames`:

- `EmittedByAuthorizationEndpoint` (`[code]`): lo que el authorize acepta y responde. La usa el validador.
- `SupportedCombinations` (las 7): lo que el discovery declara. La usa el builder.

Se elimino `Supported`, que era una tercera lista (`[code, token, id_token]`) **sin un solo uso** en `src`
ni en `tests`: no la consumia ni el validador ni el discovery. Tres listas parcialmente solapadas era
justo el terreno donde el bug se escondia.

**Decision de alcance.** El discovery **sigue anunciando** las siete combinaciones. Anunciarlas es lo que
permite que una aplicacion real valide la metadata al arrancar sin fallar, que es el objetivo del
proyecto; implementarlas es D-019 y sigue fuera de alcance. Lo que cambia es que el endpoint ya no
**acepta** lo que no puede **emitir**: la paridad se mantiene donde es inocua y se corrige donde miente.

**El criterio, en una frase:** un cliente debe recibir un error honesto antes que una respuesta que no
puede usar. Reimplementar el flow implicito (D-019) es lo que haria innecesario este rechazo; hasta
entonces, `unsupported_response_type` es la respuesta correcta.

**Cobertura.** 6 tests en rojo, uno por combinacion implicita, mas 6 de integracion por HTTP que
comprueban ademas que la redireccion **no** lleva `code=`, y un test que ata que las dos listas sigan
siendo distintas. Verificado revirtiendo el arreglo: 6 fallos.

### D-043 — `select_account` se anuncia, se acepta y no se hace

`prompt=select_account` aparece en `prompt_values_supported` porque el servidor real lo declara, y el
validador lo acepta. Pero `AuthorizationInteraction.Decide` no tiene rama para el: cae en la final y
concede en silencio con el usuario de la sesion. Sin sesion, pide login.

**Por que no se implementa la pantalla de eleccion.** El mock tiene una pantalla de login con un
`datalist` de `users.json`. Reutilizarla para `select_account` daria al usuario una lista de cuentas
**sin indicarle que el prompt no se esta honrando**, y el resultado seria peor que no implementarlo:
parece funcionar y no cumple lo que el cliente pidio. Es el mismo criterio de D-042, al reves: ahi se
rechaza una peticion que no se puede cumplir; aqui se acepta una que se puede cumplir, pero de otra
manera.

**Decision.** Se deja como esta, documentado junto al codigo y fijados con tres tests: con sesion
concede, sin sesion pide login, y el usuario concedido es el de la sesion. Un solo test
(`SelectAccountSeTrataComoSinPrompt`) fijaba la mitad del comportamiento y dejaba la otra mitad libre.
Ahora los tres casos estan escritos, de modo que **cambiarlo sea un acto deliberado y no un efecto
colateral**. Si algun dia se implementa, el punto de entrada es `AuthorizationStep`, que ya distingue
`Consent` de `Grant` y admite un paso mas sin tocar el endpoint.

### D-044 — Las factorias de `TokenRequest` cierran D-024

D-024 pidio un value object para los once parametros posicionales de `TokenRequest` y dejo constancia de
que un simple cambio de firma no bastaba. La forma que se elige no es un value object sino **factorias
con nombre** sobre el mismo record: `ForClient`, `ForAuthorizationCode`, `ForPassword`, `ForRefreshToken`
y `ForPoll`. Se descarta el value object porque no hace falta un tipo nuevo para eliminar el error que
D-024 describia.

**El riesgo concreto.** Once parametros, nueve de ellos `string?` contiguos. Transponer `userName` y
`password`, o `code` y `refreshToken`, **compila sin un solo aviso** y produce un fallo en tiempo de
ejecucion, en el camino que autentica al usuario. El compilador no puede verlo porque los tipos son
identicos; solo un test lo ve.

**Por que factorias y no un record por grant.** Un `AuthorizationCodeRequest` y un `PasswordRequest`
habrian hecho el constructor a prueba de transposiciones, pero cada grant con su tipo propio empuja el
despacho de `TokenEndpointService` hacia un switch de tipos, y el dispatcher dejaria de ser "no conoce los
detalles de ningun grant" (D-009). Las factorias conservan un unico record y un unico dispatcher: lo
que cambian es **como se llama**, no quien decide.

**La caracterizacion primero, y encontro algo.** `TokenRequestMappingTests` fija, campo por campo, que
cada valor de `TokenEndpointRequest` cae en su slot, con un grant espia que captura la peticion. Al
escribirlo aparecio una confusion mia sobre `ScopesRequested`: asumi que sin scopes la marca valia
`true`. **No: vale `false`, y es lo correcto.** La marca significa "el cliente pidio un narrowing", no
"el mock concedio algo"; darla por cierta haria que una peticion sin scope invalidara los scopes
concedidos en lugar de conservarlos. El test quedo escrito con la semántica del codigo y su doc
explica el porque, que es lo que evita que otro lo "arregle" en la direccion contraria.

**Una construccion posicional sobrevive, a proposito.** `TokenEndpointService` es el unico sitio que
mapea los once campos, y es precisamente el que la caracterizacion cubre. Ahi el constructor posicional
es correcto porque el mapeo es el objeto del metodo, y el comentario lo dice para que no se lea como un
olvido.

**Nota sobre el filtro de tests.** Bajo Testing Platform, `dotnet test --filter` se ignora en silencio
(avisa con `MTP0001` y ejecuta la suite entera). El filtro real es el nativo de xunit v3:
`dotnet run --project tests/OidcMock.UnitTests -- -class "*NombreDeLaClase*"`. Un filtro que no filtra
devuelve un verde que no es verde.

## Etapa 16: licencia abierta y un solo comando para publicar

### La licencia MIT ya estaba

**No se anade nada.** `LICENSE` es MIT desde `e3fbfc6`, con copyright de Chienwei82. Se comprueba el
contenido y se declara en el README, porque un proyecto que quiere quedar abierto comunica la licencia
en la portada y no solo en un fichero que nadie mira. El texto de la licencia **no se modifica**: es el
canonico de la FSF y reescribirlo para "dejarlo mas claro" lo dejaria de ser MIT.

### `scripts/publish.py`: un comando para build, tests y publicacion

Habia tres scripts que se invocaban por separado (`test.sh`, `publish.sh` y los comandos sueltos de
`dotnet publish`). El flujo de "dejar esto listo para usar" es siempre el mismo, asi que se mete en uno.

- **Es un wrapper, no una reimplementacion.** Sigue llamando a `dotnet build` y `dotnet publish`; no
  reimplementa nada de MSBuild. El valor esta en la secuencia, el color y el diagnostico.
- **No publica si el build falla o los tests estan en rojo.** Publicar con pruebas en rojo publicaria
  algo que no se sabe que funciona, y el nombre del directorio (`artifacts/publish`) haria creer lo
  contrario.
- **El log de cada paso va a `artifacts/publish/logs/` y se imprime un resumen, no el volcado.** Un
  fallo enseña las lineas con `: error ` o `[FAIL]`, que es lo que hace falta para diagnosticar.
- **El resumen de tests se lee con expresion regular, no partiendo por comas.** El runner de Testing
  Platform **colorea su salida con codigos ANSI**, asi que la linea llega sucia; la primera version
  devolvia `? tests, ? fallos` porque `Total:` venia precedido de escapes. Se filtra tambien el aviso
  `MTP0001`, que es ruido conocido (D-044).
- **Se borra el directorio de salida antes de publicar.** Un publish parcial de una version anterior
  dejaria binarios viejos junto a los nuevos, que es la forma clasica de distribuir de mas.
- **Color con salida a TTY y respeta `NO_COLOR`.** A alguien que redirige la salida a un fichero no le
  llegan secuencias de escape.
- **`publish.sh` y `test.sh` se quedan.** El wrapper es lo que se usa a diario; los otros dos siguen
  sirviendo para publish a pelo o para los tests sin publicar, y borrarlos seria quitar opciones.

### La salida de publicacion nunca estuvo en git

Se pidio sacar `publish/` del repositorio. **No hacia falta: `git log --all -- publish/` y
`git log --all -- artifacts/` salen vacios**, es decir, nunca hubo nada que sacar. `artifacts/` ya
estaba en `.gitignore` desde antes. Lo que si se hace es **blindarlo** para que no aparezca por
costumbre: `publish/` y `__pycache__/` se anaden a `.gitignore`, y `publish` a `.dockerignore` para que
una publicacion local no engorde el contexto de la imagen. El script avisa si encuentra un `publish/`
en la raiz, porque ahi no es donde escribe.

### Verificacion

El binario publicado se arranco de verdad, no solo se compilo: `dotnet publish` en `linux-x64`, copia de
`config/` a un directorio aparte (D-033: los JSON no viajan en la salida), arranque con
`OidcMock__Serving__AllowHttp=true` y `HttpPort=5199`. Discovery 200, JWKS 200, y el bug de D-042
verificado en el binario: `response_type=id_token token` responde 302 con
`error=unsupported_response_type` al `redirect_uri` registrado.

## Script manual scripts/oidc-test.py

- **Stdlib solo, sin tests ni paquete nuevo.** Es herramienta manual fuera del
  flujo TDD/build (como `publish.py`): replica la URL de login con su doble
  codificacion observada y desglosa el retorno (`#code/id_token/session_state`)
  con color ANSI y JWT decodificado sin verificar firma (solo inspeccion).
- **Tus valores GAUDI como defecto** (host, client_id, nonce fijo, challenge,
  landing). Flags > env `OIDC_*` > `--config` JSON > defecto.
- **Valores fijos, no aleatorios:** nonce y challenge por defecto son los de tu
  URL; `--random-nonce`/`--new-pkce` generan otros cuando quieras probar.
- Credenciales/api-keys: el usuario controla su commit (pedido explicito).

## Refactor de legibilidad de scripts/ (2026-10-07)

Cuatro scripts mejorados en la rama `chore/incluir-scripts`: `test.sh`, `publish.sh`, `publish.py` y
`oidc-test.py`. Criterio: **mismo comportamiento observable** (CLI, textos y formato de salida),
mejor forma interna.

- **Sin tests nuevos a proposito.** Son herramientas manuales de desarrollo, fuera del flujo TDD
  (mismo criterio ya decidido para `publish.py` en la etapa 16). La verificacion fue por
  equivalencia: la salida de `oidc-test.py` se capturo en 7 escenarios antes del cambio y se comparo
  byte a byte despues (`diff -r`), y `./scripts/publish.py` se ejecuto entero.
- **`oidc-test.py` reescrito, no retocando:** nombres que revelan intencion (`C`→`Console`,
  `kv`→`print_field`, `cut`→`abbreviate`, `jwt`→`parse_jwt`, `stamp`→`format_timestamp`,
  `stable`→`unquote_until_stable`), una sentencia por linea, type hints y docstrings, y los numeros
  magicos (anchos de columna, truncados, longitudes PKCE) como constantes nombradas. Tambien se
  elimino un marcador `#PART2` suelto (resto de una edicion anterior).
- **Bug real hallado y corregido (`fix:` aparte):** en `publish.py`, `report_failure` estaba definida
  *despues* de `if __name__ == "__main__": sys.exit(main())`, asi que al fallar un build el script
  revienta con `NameError` en vez de imprimir el resumen del fallo — justo el camino de error para el
  que existe la funcion. Demostrado en rojo antes de moverla.
- **`publish.py` ademas:** `megabytes`→`size_in_mib` (devolvia MiB, no MB), `test_summary` con
  `finditer` y grupos nombrados en vez de `findall` + indices `[0]`/`[1]`, y un unico cronometro
  (los segundos ya venian de `run()`; se descartaban y se cronometraba otra vez).
- **Shell scripts:** `readonly`, las suites y los runtimes como arrays nombrados (sin repetir la
  lista en el `for`), comentarios de uso y el porque del `rm -rf` previo.

## Adaptacion a GAUDI: flujo hibrido y rutas de entrada (2026-10-07)

Para correr la prueba del script contra el mock sustituyendo al servidor real de la empresa
(GAUDI), con la captura de su URL de salida como especificacion.

### Lo que se implemento y por que

- **`response_type=code id_token` pasa a emitirse** (`EmittedByAuthorizationEndpoint`). La lista de
  lo aceptado y la del discovery siguen siendo dos (D-042); ahora la diferencia es que el hibrido
  si se responde y las combinaciones con access token (`code token`, `id_token token`, ...) siguen
  rechazandose con `unsupported_response_type`.
- **La respuesta hibrida es exactamente la captura:** `#code=<HEX64>-1&id_token=...&session_state=...`
  en el fragmento, sin el `?` que antes quedaba tras el `#` (bug de `ToQueryString`) y **sin `iss`**:
  el servidor real lo omite aqui pese a anunciar
  `authorization_response_iss_parameter_supported`. El mock imita lo que hace, no lo que dice; el
  flujo de codigo conserva el `iss` que ya tenia fijado.
- **Formatos observados, no opacos:** code en 64 hexadecimales mayuscula con sufijo `-1`,
  `session_state` como `<43 base64url>.<32 HEX>` y `sid` en 32 hexadecimales mayuscula. Son
  visibles en la URL de retorno y en el id_token, que es justo lo que la prueba desglosa.
- **Claims de sesion del id_token:** `sid` (la sesion del navegador que hizo login, enhebrada por
  el `AuthorizationCode` hasta el canje), `amr=["sc"]`, `acr="possessionorinherence"` e
  `idp="local"`. Los tres ultimos son valores **fijos** observados en la captura: el mock no
  autentica con tarjeta, imita la forma para que la aplicacion no vea diferencia.
- **Rutas de entrada:** `/Account/Login?ReturnUrl=` (login con `ReturnUrl` local obligatorio; una
  URL absoluta se rechaza con 400 para no ser un open redirect) y el alias
  `/connect/authorize/callback` del authorize, que es el destino del `ReturnUrl` en el servidor
  real. El HTML del login es propio; el real es el de ASP.NET Identity y no se imita.
- **`response_mode` por defecto:** fragmento cuando la respuesta lleva tokens y query solo para
  `code` (Multi Response Type Encoding 3). `response_mode=query` con un hibrido se rechaza con
  `invalid_request`.
- **`UserAuthenticator`** concentra la comparacion de credenciales en tiempo constante que usan el
  authorize y la nueva ruta de login.

### Configuracion de prueba

- Cliente publico `fb02079c-3143-49e6-a776-dd9b002388d2` con PKCE obligatorio y el redirect_uri
  falso `http://localhost:5173/pruebas/ves/landing` (no hay servidor atras; solo tiene que existir
  registrado). Usuario `prueba` / `Prueba123!` con `sub` `01-2222-3333`, **dato falso de prueba**.
- `scripts/oidc-test.py` apunta por defecto a `https://localhost:5443/personafisica/` con ese
  cliente, un par PKCE fijo (el `code_verifier` vive en los defaults, para poder canjear el code) y
  el redirect falso. `--host`/`--client-id` lo re-apuntan al servidor real.
- Los tests dejan de fijar el numero de usuarios del `config/` y fijan los concretos: agregar uno
  de ejemplo no debe romperlos, igual que ya hacia `ConfigCoherenceTests` con los clientes.
- **El subject es configurable en ambos lados.** El `sub` del usuario vive en `config/users.json` y
  el script lo contrasta con `--subject` / `OIDC_SUBJECT` / `subject` en el `--config` (por defecto
  `01-2222-3333`, el del usuario `prueba`, dato falso). Cambiar el subject no toca codigo: se cambia
  en la configuracion y el desglose senala si el `sub` del id_token coincide o no.

## Identidad editable en las pantallas y UI Material You (2026-10-07)

Para que el mockup no dependa de credenciales: quien usa el mock elige y edita la identidad que
viaja en el JWT, y las pantallas se pintan con los tokens Material You dark del prototipo de
`docs/UI-Prototype/` (que se versiona aqui como fuente de verdad visual).

### Supuestos que fijan el contrato observable

- **Un mockup no autentica.** La pantalla de identidad pide `sub` y un input por claim del perfil
  precargado; no hay usuario ni contrasena y no se valida nada. `Aceptar` concede en un paso (el
  consentimiento solo aparece con `prompt=consent` con sesion) y `Denegar` simula un fallo de
  autenticacion, que el cliente ve como `error=access_denied` por el `redirect_uri` (RFC 6749
  4.1.2.1). En `/Account/Login` `Denegar` re-muestra la pantalla con el error simulado: ahi no hay
  un cliente al que devolver un `access_denied`.
- **Un POST sin `action` concede.** Solo `action=deny` deniega: los scripts y el arnes que no pasan
  por la pantalla esperan obtener su codigo sin saber que boton existe.
- **Semantica del POST: perfil base + overrides.** El perfil base se elige con `perfil=<username>`
  (enlaces GET de la pantalla; por defecto el primer usuario de `users.json`), el `sub` tecleado
  manda sobre el del perfil y cada campo `claim.<nombre>` reemplaza su valor. Un campo de claim
  vacio borra el claim del JWT; si el formulario no trae ningun campo de claim, la identidad es la
  del perfil tal cual.
- **Los valores que parsean como JSON conservan su tipo** (`true`, `123`, `["rol"]`); el resto viaja
  como cadena. Las cadenas se editan sin comillas.
- **Identidad sintetica en memoria (`OverlayUserStore`).** El usuario recordado desde la pantalla
  sombrea al de `users.json` (mismo `sub`) para que el canje, userinfo y refresh devuelvan lo que se
  edito, sin tocar el pipeline, que sigue hablando de `IUserStore`. La identidad sintetica nace con
  `UserName == Subject`; el perfil solo aporta los valores iniciales.
- **El grant `password` sigue autenticando de verdad** contra `users.json`: es protocolo, no
  pantalla. Es el unico consumidor de la comparacion en tiempo constante (en
  `PasswordGrantHandler`), y con la pantalla sin credenciales desaparecieron `UserAuthenticator` y
  `SignInRequest`.
- **Claims editables = claims de usuario.** Los de protocolo (`aud`, `iss`, `exp`, `nonce`, `sid`,
  `amr`, `acr`, `idp`, ...) los pone el token factory y no se editan en la pantalla.

### Material You dark

- `MockStyles` concentra la tabla de tokens de `docs/UI-Prototype/index.html` (color, forma,
  tipografia, elevacion, movimiento) y los componentes de pantalla; las seis pantallas HTML
  (identidad, `/Account/Login`, consentimiento, sesion cerrada, `form_post` y `check_session`) la
  referencian en vez de llevar su propio CSS. Sin Google Fonts: la tipografia declara
  `'Inter', system-ui, sans-serif` y usa la del sistema si Inter no esta instalada.
- **El branding del cliente manda sobre `--md-primary`** (contrato ya fijado por tests: el color del
  cliente aparece en la pantalla). Como los brandings de `config/` son oscuros, `--md-on-primary`
  va en claro para conservar el contraste del boton primario; sin branding manda la pareja de la
  paleta del prototipo.


## D-045 · Consentimiento memorable (2026-10-07)

**Decision: el consentimiento aprobado se recuerda en memoria por cliente + usuario, y lo recordado
cubre solo sus scopes.** Cierra el pendiente "Consentimiento memorable: `prompt=consent` vuelve a
preguntar en cada authorize".

- **`IConsentStore` / `InMemoryConsentStore`**: el conjunto de scopes aprobados crece con cada
  aprobacion de la pantalla de consentimiento. Denegar no deja constancia (la proxima vez vuelve a
  preguntar), y un scope nuevo tampoca esta cubierto: vuelve a mostrar la pantalla.
- **Solo la pantalla de consentimiento deja constancia.** La concesion en un paso de la pantalla de
  identidad autentica, no consiente; si contara como consentimiento, `prompt=consent` dejaria de
  preguntar tras cualquier login y el prompt no significaria nada.
- **Desviacion consciente de OIDC Core 3.1.2.1**, que pide volver a mostrar el consentimiento con
  `prompt=consent`. El mock es una herramienta de desarrollo: el flujo repetido (probar la app una y
  otra vez) no debe obligar a hacer click en cada authorize. Es el mismo espiritu que "un mockup no
  autentica": lo que se optimiza es la iteracion, no la seguridad.
- **En memoria, como los codigos y los tokens** (regla 3): reiniciar el mock borra lo recordado.

