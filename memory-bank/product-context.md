# Product Context — OidcMock

## Para qué

El usuario integra contra el IdP real del BCCR (Banco Central de Costa Rica) y necesita probar
localmente, sin red y sin credenciales reales: **autorización, tokens, claims y refresh** en
entornos de desarrollo, CI y demos.

## Para quién

- Desarrolladores de aplicaciones .NET que usan `Microsoft.Identity.Web` / `AddOpenIdConnect`.
- Equipos que necesitan escenarios de error (token expirado, `invalid_client`, scope no permitido)
  reproducibles a voluntad.

## Qué es observable en el exterior (paridad con el discovery real)

- Rutas bajo el prefijo `/personafisica/`: `/.well-known/openid-configuration`,
  `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/endsession`,
  `/connect/checksession`, `/connect/revocation`, `/connect/introspect`,
  `/connect/deviceauthorization`, `/connect/ciba`, `/connect/par`.
- JWKS en `/.well-known/openid-configuration/jwks`; firma **RS256**; `subject_types_supported: public`.
- `authorization_response_iss_parameter_supported: true` (el `iss` también viaja en la respuesta).
- `code_challenge_methods_supported: plain` y `S256`.
- `client_secret` en **texto plano** y `password` de usuario en texto plano: es un mock local,
  no hay rotación ni hashing.
- Claims con nombres libres, incluidos los del BCCR: `codTipoId`, `Bccr.IdUsuario`,
  `Bccr.IdEntidad`, `Bccr.CodTipoId`, `Bccr.negocio`, `documentofva`, `nombre`, `full_name`.

## Qué NO hace (límites explícitos)

- No hay base de datos, ni migraciones, ni ORM.
- No hay cifrado, hash de secretos ni auditoría: el almacenamiento en claro es intencional.
- No se validan credenciales reales ni se contacta el IdP del BCCR.
- No se implementan `request` objects firmados, DPoP ni mTLS de cliente en las primeras etapas
  (se anuncian en el discovery, se añaden cuando la etapa correspondiente toque).
- No es un IdP de producción: **nunca** exposarlo fuera de `localhost`.
