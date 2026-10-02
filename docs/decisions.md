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
