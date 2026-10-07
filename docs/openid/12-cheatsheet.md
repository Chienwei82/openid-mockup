# 12 · Cheatsheet (una página)

Todo lo esencial del OidcMock en una sola hoja. Detalle completo en los otros documentos de la serie.

## Arrancar

```bash
# HTTPS con el certificado de desarrollo (https://localhost:5443/personafisica/)
dotnet run --project src/OidcMock.Host

# HTTP plano (contenedor / proxy)
dotnet run --project src/OidcMock.Host -- --OidcMock:Serving:UseHttps=false --OidcMock:Serving:AllowHttp=true

# Fijar el emisor (debe coincidir EXACTO con el que use el cliente, con barra final)
dotnet run --project src/OidcMock.Host -- --OidcMock:Issuer=https://identidad.local/personafisica
```

Docker: `docker run --rm -p 8080:8080 -v "$PWD/config:/app/config" oidcmock`.
Publicar: `./scripts/publish.py`. Probar: `./scripts/test.sh` (build Release + 3 suites).

## Base y opciones clave

| Opción | Defecto | Para qué |
| --- | --- | --- |
| `OidcMock:PathBase` | `/personafisica` | Prefijo de todas las rutas |
| `OidcMock:Issuer` | del host | Emisor anunciado (con barra final) |
| `OidcMock:BaseUrl` | — | URL pública (fija path + issuer); manda sobre las dos anteriores |
| `OidcMock:SessionLifetime` | `08:00:00` | Vida de la sesión del navegador |
| `OidcMock:ConfigDirectory` | `./config` | Dónde están los JSON |
| `OidcMock:ReloadOnChange` | `true` | Recarga de los JSON al cambiar |

## Endpoints (bajo `<base>` = `/personafisica`)

| Método | Ruta | Auth |
| --- | --- | --- |
| `GET` | `/.well-known/openid-configuration` | — |
| `GET` | `/.well-known/openid-configuration/jwks` | — |
| `GET`/`POST` | `/connect/authorize` · `/connect/authorize/callback` | sesión |
| `GET`/`POST` | `/Account/Login?ReturnUrl=` | — |
| `POST` | `/connect/token` | cliente |
| `GET`/`POST` | `/connect/userinfo` | `Bearer` |
| `POST` | `/connect/introspect` | cliente |
| `POST` | `/connect/revocation` | cliente |
| `GET`/`POST` | `/connect/endsession` | — |
| `POST` | `/connect/par` | cliente |
| `POST` | `/connect/deviceauthorization` · `/connect/ciba` | cliente |
| `GET` | `/connect/checksession` | — |

Auth de cliente: `client_secret_basic` (encabezado `Authorization`) o `client_secret_post` (cuerpo).
El encabezado **manda** sobre el cuerpo (RFC 6749 § 2.3.1). Un cliente público entra solo con `client_id`.

## Tokens

| Token | forma | vida (ej. `web-app-spa`) | cuándo |
| --- | --- | --- | --- |
| `authorization_code` | opaco, un uso | 5 min | redirect del authorize |
| `access_token` | JWT RS256 | 30 min | token endpoint (`aud` = `client_id`) |
| `id_token` | JWT RS256 | 30 min | solo con scope `openid` |
| `refresh_token` | opaco, un uso, rota | 8 h | solo con `offline_access` |

`token_type` siempre `Bearer`. El `iss` del JWT lleva **barra final**. El `kid` sale del JWKS.

## Parámetros

**`/connect/authorize`**: `client_id`, `redirect_uri`, `response_type`, `scope`, `state`, `nonce`,
`code_challenge`, `code_challenge_method`, `prompt`, `response_mode`, `request_uri`.
- `response_type` válidos **de verdad**: `code` y `code id_token`. Los demás → `unsupported_response_type`.
- `response_mode`: `query` (solo con `code`), `fragment`, `form_post`.
- `prompt`: `none`, `login`, `consent`, `select_account`.
- PKCE `method`: `plain` (por defecto si se omite) o `S256`.

**`/connect/token`**: `grant_type`, `code`, `redirect_uri`, `code_verifier`, `refresh_token`, `scope`,
`username`, `password`, `device_code`/`auth_req_id`.

| `grant_type` | Emite |
| --- | --- |
| `authorization_code` | access + id (+ refresh con `offline_access`) |
| `refresh_token` | access (+ id si conserva `openid`), refresh **nuevo** |
| `client_credentials` | solo access (confidenciales; `sub` = `client_id`) |
| `password` | access + id (+ refresh) |
| `urn:ietf:params:oauth:grant-type:device_code` | access (+ id) por sondeo |
| `urn:openid:params:grant-type:ciba` | access (+ id) por sondeo |

## Errores (código → HTTP)

| `error` | HTTP | Típico de |
| --- | --- | --- |
| `invalid_request` | 400 | falta/estorba un parámetro |
| `invalid_client` | **401** | credenciales de cliente malas/ausentes |
| `invalid_grant` | 400 | code/refresh inválido, PKCE no casa, familia revocada |
| `unauthorized_client` | 400 | grant no permitido para el cliente |
| `unsupported_grant_type` | 400 | grant desconocido |
| `invalid_scope` | 400 | scope no permitido o inexistente |
| `unsupported_response_type` | 400 | `response_type` no respondido (p.ej. implícito) |
| `access_denied` | 400 | el usuario denegó |
| `invalid_token` | 401 | `/userinfo` con token malo (reto Bearer) |
| `authorization_pending` / `slow_down` / `expired_token` | 400 | ciclo de sondeo (Device/CIBA) |
| `invalid_request_uri` | 400 | `request_uri` de PAR desconocido o caducado |

En el authorize, los errores de `client_id`/`redirect_uri` se muestran **en el endpoint** (no se
redirigen) para evitar open redirect; el resto viajan por el `redirect_uri`.

## Datos de ejemplo

| Cliente | tipo | secreto | grants |
| --- | --- | --- | --- |
| `web-app-spa` | público (PKCE) | — | code, refresh, device, ciba |
| `web-app-confidencial` | confidencial | `super-secreto-web` | code, refresh |
| `backend-service` | confidencial | `super-secret-backend` | client_credentials, password, refresh, ciba |
| `fb02079c-…388d2` | público (Pruebas Open ID) | — | code, refresh |

| Usuario | Contraseña | `sub` |
| --- | --- | --- |
| `jperez` | `Passw0rd!` | `user-persona-fisica` |
| `empresa-demo` | `Passw0rd!` | `user-persona-juridica` |
| `prueba` | `Prueba123!` | `01-2222-3333` |
| `admin` | `Admin123!` | `user-administrador` |

> La pantalla de identidad deja **editar** `sub` y `claim.*` a propósito: no hace falta un usuario real.

## Comandos útiles

```bash
./scripts/test.sh                 # build Release + unit + integration + compat
./scripts/publish.py              # build + tests + publish (linux-x64 y win-x64)
./scripts/oidc-test.py --build    # URL de entrada del flujo híbrido de GAUDI
./scripts/oidc-test.py "<url-retorno>"   # desglose del fragmento + claims
```

## Recordatorio

Mock de **desarrollo local**: secretos en claro, estado en memoria (reiniciar = olvidar), flujos
Device/CIBA/check_session **simulados**. **Nunca** exponerlo fuera de `localhost`.

Índice de la serie: **[README](README.md)** · Matriz de paridad: **[11](11-matriz-de-paridad.md)**.
