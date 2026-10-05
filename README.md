# OidcMock

Servidor OpenID Connect **falso** para desarrollo local y offline. Imita el comportamiento observable
del servidor real del BCCR (`https://oauth2.bccr.fi.cr/personafisica/`) para que una aplicación
apunte al mock **sin cambios de código**: mismo discovery, mismos endpoints, mismos nombres de
scopes y claims, mismos errores de protocolo.

No hay base de datos: la configuración son archivos JSON y los tokens, códigos y refresh tokens viven
en memoria. Al reiniciar el proceso, se olvida todo.

- .NET 10 / C# 14
- Solo el framework + `Microsoft.IdentityModel.JsonWebTokens`
- Sin dependencias de terceros en producción
- **MIT**: úsalo, modifícalo y compártelo sin pedir permiso. Ver [`LICENSE`](LICENSE).

---

## Qué es y qué no es

Es un emulador de protocolo, no un proveedor de identidad real. Sirve para **desarrollar y probar** la
integración de una aplicación: login, renovación de sesión, logout, validación de tokens contra el
JWKS. Los secretos que hay en `config/` son de ejemplo y están pensados para estar en un repositorio.

### Alcance

| Capacidad | Estado | Notas |
| --- | --- | --- |
| Discovery (`.well-known/openid-configuration`) | Implementado | Emisor con barra final, como el real |
| JWKS (RS256) | Implementado | Clave propia en `config/signing-key.pem`, generada en el primer arranque |
| `authorization_code` + PKCE (`plain` y `S256`) | Implementado | El refresh token exige `offline_access` (OpenID Connect Core 11) |
| `refresh_token` | Implementado | Con rotación: un refresh token canjeado no se puede reutilizar |
| `client_credentials` | Implementado | Solo para clientes confidenciales, sin usuario |
| `password` | Implementado | Solo para clientes que lo permitan |
| `userinfo` | Implementado | Con `WWW-Authenticate` en el 401 |
| `introspect` y `revocation` (RFC 7662 / 7009) | Implementado | Con autenticación de cliente |
| `end_session` (RP-Initiated Logout) | Implementado | Valida `id_token_hint`, avisa por frontchannel logout |
| Pushed Authorization Requests (RFC 9126) | Implementado | Se anuncia y **se resuelve**: `request_uri` reconstruye la petición |
| Device Authorization Grant (RFC 8628) | **Simulado** | Emite `device_code`/`user_code` y responde `authorization_pending` / `slow_down` / `expired_token` según el RFC 8628 3.5, pero no hay pantalla de identificación |
| CIBA | **Simulado** | Solo la API de *poll* del token endpoint, con el mismo ciclo de sondeo; sin entrega push ni pantalla de aprobación |
| `check_session_iframe` | **Simulado** | Se sirve la página, no implementa OPiFrame (RFC 6614) |
| Implicit (`response_type=id_token token`) | **Anunciado, no implementado** | El discovery lo declara por paridad con el real, pero el authorize responde `unsupported_response_type`: el mock solo emite `code` |
| `select_account` | **Anunciado, no implementado** | Se acepta el valor pero el flujo es el mismo que sin él: concede con el usuario de la sesión. Ver D-043 |
| `request` objects firmados, DPoP, mTLS (`ClientCertificate`) | **No implementado** | |
| Frontchannel logout | Parcial | Se llama al `frontchannel_logout_uri`; no hay aviso de sesión de backchannel |
| Multitenancy / usuarios reales | Fuera de alcance | Un solo conjunto de clientes, usuarios y scopes, el del `config/` |

Los puntos simulados existen para que el discovery no difiera del real y una aplicación que valide la
metadata al arrancar no falle. **No se usen para validar seguridad.**

---

## Cómo correrlo

### Con `dotnet run`

```bash
dotnet run --project src/OidcMock.Host
```

Por defecto escucha **HTTPS en `https://localhost:5443/personafisica/`** con el certificado de
desarrollo. Si no lo tienes, genéralo:

```bash
dotnet dev-certs https --trust
```

Para arrancar en HTTP plano (lo habitual en un contenedor o detrás de un proxy):

```bash
dotnet run --project src/OidcMock.Host \
  -- --OidcMock:Serving:UseHttps=false --OidcMock:Serving:AllowHttp=true
```

Con el emisor deducido del host, el discovery queda coherente solo. Para fijarlo (por ejemplo, si el
mock va detrás de un dominio):

```bash
dotnet run --project src/OidcMock.Host -- \
  --OidcMock:Issuer=https://identidad.local/personafisica
```

Lo mismo con variables de entorno:

```bash
OidcMock__ConfigDirectory=/etc/oidcmock \
OidcMock__Serving__UseHttps=false \
OidcMock__Serving__AllowHttp=true \
OidcMock__Serving__HttpPort=8080 \
OidcMock__Issuer=https://identidad.local/personafisica \
dotnet run --project src/OidcMock.Host
```

**Importante:** el `Issuer` anunciado tiene que ser exactamente el que use el cliente. Si no, el
`id_token` no valida porque el `iss` no coincide, y el discovery no cuadra.

### Con Docker

El `Dockerfile` construye una imagen autocontenida, publica en el puerto 8080 y deja la configuración
en `/app/config` para poder montarla encima.

```bash
# Construir la imagen
docker build -t oidcmock .

# Correr con la configuración del repositorio
docker run --rm -p 8080:8080 oidcmock

# Correr con tu propia configuración (clientes, usuarios, scopes, clave de firma)
docker run --rm -p 8080:8080 -v "$PWD/config:/app/config" oidcmock
```

### Publicación autocontenida

```bash
./scripts/publish.py          # compila, pasa los tests y publica (lo hace todo)
```

Deja un binario de un solo archivo, sin necesidad del runtime de .NET, en
`artifacts/publish/linux-x64` y `artifacts/publish/win-x64`. Si prefieres solo publicar, sin volver a
probar, `./scripts/publish.py --skip-tests`; el script `scripts/publish.sh` sigue siendo la publicación
a pelo, sin build ni tests.

### Entrar

El login es una pantalla HTML con la lista de usuarios de `config/users.json`. Con los datos de ejemplo:

| Usuario | Contraseña |
| --- | --- |
| `jperez` | `Passw0rd!` |
| `empresa-demo` | `Passw0rd!` |
| `admin` | `Admin123!` |

---

## Configuración

La configuración son cuatro archivos en un directorio, `config/` por defecto, que se cambia con
`OidcMock:ConfigDirectory` o `OidcMock__ConfigDirectory`. Los tres JSON se recargan en caliente
(`OidcMock:ReloadOnChange=false` para desactivarlo); la clave de firma, no.

```
config/
├── clients.json        # clientes registrados
├── users.json          # usuarios que pueden entrar
├── scopes.json         # scopes y los claims que proyecta cada uno
└── signing-key.pem     # clave RSA: se genera sola la primera vez si no existe
```

> `signing-key.pem` se genera en el primer arranque si falta. Si montás el directorio de configuración
> como solo lectura, deja el PEM de antemano: si no, el mock arranca pero avisa por log de que firmará
> con una clave distinta en cada reinicio.

### `clients.json`

```json
{
  "clients": [
    {
      "client_id": "mi-app",
      "client_secret": "mi-secreto",
      "redirect_uris": ["https://localhost:5001/signin-oidc"],
      "post_logout_redirect_uris": ["https://localhost:5001/"],
      "frontchannel_logout_uri": null,
      "allowed_grant_types": ["authorization_code", "refresh_token"],
      "allowed_scopes": ["openid", "profile", "email", "offline_access"],
      "require_pkce": true,
      "require_client_secret": true,
      "token_lifetimes": {
        "access_token": "00:30:00",
        "id_token": "00:30:00",
        "refresh_token": "08:00:00",
        "authorization_code": "00:05:00"
      },
      "branding": {
        "display_name": "Mi App",
        "logo_url": null,
        "primary_color": "#00695C"
      }
    }
  ]
}
```

| Campo | Qué significa |
| --- | --- |
| `client_secret` | `null` = cliente público (SPA). Sin secreto, `require_client_secret` debe ser `false` |
| `allowed_grant_types` | `authorization_code`, `refresh_token`, `client_credentials`, `password`, `urn:ietf:params:oauth:grant-type:device_code`, `urn:openid:params:grant-type:ciba` |
| `allowed_scopes` | Scopes que el cliente puede pedir. Sin `offline_access` no recibirá refresh tokens |
| `require_pkce` | Exige `code_challenge` en el authorize |
| `frontchannel_logout_uri` | Si está, el `end_session` avisa a esa URL |

### `users.json`

```json
{
  "users": [
    {
      "sub": "user-persona-fisica",
      "username": "jperez",
      "password": "Passw0rd!",
      "claims": {
        "name": "Juan Pérez",
        "given_name": "Juan",
        "email": "jperez@example.cr",
        "Bccr.IdUsuario": "us-88213",
        "role": ["persona-fisica"]
      }
    }
  ]
}
```

`sub` es el identificador estable; `claims` se proyecta al `id_token` y al `userinfo` según los scopes
concedidos. La contraseña va **en claro**: es un mock para desarrollo.

### `scopes.json`

```json
{
  "scopes": [
    { "name": "openid", "claims": ["sub"] },
    { "name": "email", "claims": ["email", "email_verified"] }
  ]
}
```

Los nombres de scope y de claim son los del servidor real (`nombre`, `documentofva`, `Bccr.IdEntidad`,
`custom.profile`, `roles`, …) para que una aplicación que los pida funcione igual contra el mock.

### Opciones (`appsettings.json`, variables de entorno o `--OidcMock:…`)

| Opción | Por defecto | Para qué |
| --- | --- | --- |
| `OidcMock:PathBase` | `/personafisica` | Prefijo de todas las rutas |
| `OidcMock:Issuer` | se deduce del host | Emisor anunciado en el discovery |
| `OidcMock:SessionLifetime` | `08:00:00` | Vigencia de la sesión de login en el navegador |
| `OidcMock:ConfigDirectory` | `./config` | Dónde están los JSON |
| `OidcMock:ReloadOnChange` | `true` | Recarga de los JSON al cambiar |
| `OidcMock:AllowedCorsOrigins` | `localhost:4200`, `5173`, `3000` | Orígenes de navegador con CORS |
| `OidcMock:Serving:UseHttps` | `true` | Escuchar HTTPS |
| `OidcMock:Serving:HttpsPort` | `5443` | Puerto HTTPS |
| `OidcMock:Serving:CertificatePath` | certificado de desarrollo | PFX propio |
| `OidcMock:Serving:CertificatePassword` | — | Contraseña del PFX |
| `OidcMock:Serving:AllowHttp` | `false` | Escuchar también HTTP plano |
| `OidcMock:Serving:HttpPort` | `8080` | Puerto HTTP plano |

---

## Conectar una aplicación .NET

Solo hace falta el **issuer**: el discovery, el JWKS, el token endpoint y el logout se descubren solos.

```csharp
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.Authority = "https://localhost:5443/personafisica/";
        options.ClientId = "web-app-confidencial";
        options.ClientSecret = "super-secreto-web";
        options.ResponseType = OpenIdConnectResponseType.Code;

        // Guarda los tokens en la cookie de sesion, para poder renovar la sesion mas adelante.
        options.SaveTokens = true;

        // Trae los claims de perfil y correo desde el userinfo endpoint.
        options.GetClaimsFromUserInfoEndpoint = true;

        // El refresh token hay que pedirlo explicitamente: el handler de .NET no lo anade solo.
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("offline_access");

        // Solo para desarrollo: el mock usa el certificado de desarrollo.
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();
```

```csharp
app.MapGet("/privado", () => Results.Ok()).RequireAuthorization();
```

Notas importantes:

- **El issuer termina en barra.** El discovery lo anuncia con barra y el `iss` del `id_token` también.
- **`offline_access` hay que pedirlo.** El mock solo emite refresh token si el scope está concedido
  (OpenID Connect Core 11), y el handler de ASP.NET Core no lo añade por su cuenta.
- **La renovación no es automática.** ASP.NET Core guarda el refresh token pero no lo canjea: es la
  aplicación la que llama al token endpoint con `grant_type=refresh_token` y reemite su cookie de sesión.
- **Sobre HTTP plano**, el cliente tiene que relajar las cookies de correlación y nonce, que con la
  política por defecto son `SameSite=None; Secure` y el navegador las descartaría en el `form_post` de
  vuelta:

  ```csharp
  options.CorrelationCookie.SameSite = SameSiteMode.Lax;
  options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
  options.NonceCookie.SameSite = SameSiteMode.Lax;
  options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
  ```

Para una API que valida los tokens del mock:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:5443/personafisica/";
        options.Audience = "web-app-confidencial"; // el access token lleva el client_id como aud
        options.RequireHttpsMetadata = false;
    });
```

---

## Endpoints

Todos cuelgan del `PathBase` (`/personafisica` por defecto). En la tabla, `<base>` es ese prefijo.

| Método | Ruta | Para qué | Autenticación |
| --- | --- | --- | --- |
| `GET` | `<base>/.well-known/openid-configuration` | Discovery | ninguna |
| `GET` | `<base>/.well-known/openid-configuration/jwks` | Claves públicas para validar JWT | ninguna |
| `GET` | `<base>/connect/authorize` | Login con la respuesta en el `redirect_uri` | sesión del navegador |
| `POST` | `<base>/connect/authorize` | Login y consentimiento (`form_post`) | sesión del navegador |
| `POST` | `<base>/connect/par` | Pushed Authorization Request (RFC 9126) | cliente |
| `POST` | `<base>/connect/token` | Canje de código, refresh, `client_credentials`, `password` | cliente |
| `GET`/`POST` | `<base>/connect/userinfo` | Claims del usuario del token | `Bearer` |
| `POST` | `<base>/connect/introspect` | Estado de un token (RFC 7662) | cliente |
| `POST` | `<base>/connect/revocation` | Revocar un token (RFC 7009) | cliente |
| `GET`/`POST` | `<base>/connect/endsession` | Logout iniciado por el RP | `id_token_hint` o `client_id` |
| `GET` | `<base>/connect/checksession` | Iframe de sesión (simulado) | ninguna |
| `POST` | `<base>/connect/deviceauthorization` | Device flow (RFC 8628) | cliente |
| `POST` | `<base>/connect/ciba` | CIBA, entrega `poll` | cliente |

La autenticación de cliente admite `client_secret_basic` y `client_secret_post`, que es lo que anuncia
el discovery. Es obligatoria en `token`, `par`, `introspect`, `revocation`, `deviceauthorization` y
`ciba`: un cliente público se identifica solo con `client_id`, y uno confidencial tiene que aportar su
secreto, en el cuerpo o en el encabezado `Authorization`.

---

## Estructura del repositorio

```
src/OidcMock.Core/     Dominio y casos de uso. NO referencia a ASP.NET: se puede probar sin host.
src/OidcMock.Host/     Minimal APIs, stores JSON, criptografía, configuración y logging.
tests/
  OidcMock.UnitTests/               Dominio sin E2E.
  OidcMock.IntegrationTests/        Endpoints con TestServer.
  OidcMock.ClientCompatibilityTests/  Clientes reales de ASP.NET Core contra el mock sobre Kestrel.
config/                Configuración por defecto (clientes, usuarios, scopes, clave de firma).
docs/decisions.md      Historial de decisiones con su porqué.
scripts/publish.py      Compila, prueba y publica, con color y resumen por suite.
scripts/publish.sh      Publicación autocontenida a pelo, sin build ni tests.
scripts/test.sh         Build en Release y las tres suites.
```

Las tres suites cubren cosas distintas: la unitaria prueba reglas de dominio, la de integración prueba
el HTTP del host, y la de compatibilidad usa `Microsoft.AspNetCore.Authentication.OpenIdConnect` y
`JwtBearer` **de verdad** contra el mock levantado sobre Kestrel, configurados solo con el issuer: si
el mock se aparta del protocolo en algo que un cliente real nota, lo detecta.

```bash
# Build en Release y las tres suites: lo que hay que ejecutar antes de dar algo por terminado.
./scripts/test.sh
```

El script es el comando de referencia porque compila en **Release**, que es la configuración que se publica
y donde los analizadores se portan distinto de `Debug`.

### `scripts/publish.py`

Hace los tres pasos de golpe —compilar, probar y publicar— con salida con color y un resumen por suite.
Si algo falla, dice **qué** falló y enseña las líneas relevantes del log, no el volcado entero:

```bash
./scripts/publish.py                    # build + tests + publicación (linux-x64 y win-x64)
./scripts/publish.py --skip-tests       # compilar y publicar sin probar
./scripts/publish.py --skip-publish     # build y tests, sin publicar
./scripts/publish.py --runtimes linux-x64
./scripts/publish.py -c Debug
```

Los ejecutables quedan en `artifacts/publish/<runtime>/`, autocontenidos y de un solo archivo: no
necesitan el runtime de .NET en la máquina de destino. Los logs de cada paso quedan en
`artifacts/publish/logs/`.

`artifacts/` y `publish/` están en `.gitignore`: son binarios generados y no van al repositorio.

---

## Límites conocidos

- **El estado es volátil.** Reiniciar el proceso invalida tokens, códigos y sesiones.
- **Las contraseñas y los secretos están en claro** en los JSON. Es deliberado: es un mock de desarrollo.
- **No hay multitenancy, ni rotación de claves, ni consentimiento persistente.**
- Los flujos simulados (device, CIBA, `checksession`) sirven para validar que el cliente no se rompe al
  leer el discovery, **no** para probar su comportamiento real contra esos flujos.
