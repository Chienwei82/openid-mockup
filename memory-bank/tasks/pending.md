# Tareas Pendientes

Detalle de lo que falta. El estado de alto nivel vive en [`progress.md`](../progress.md).

## Etapa 1 — cerrada ✅

- [x] T-01 `JsonFileLoader` no toca disco sin recarga.
- [x] T-02 caché del dominio mapeado (`JsonFileLoader<TFile, TDomain>`).
- [x] T-03 detección de cambios por contenido (tests sin `Thread.Sleep`).
- [x] T-04 simetría de tests (archivo ausente y caché sin recarga en users/scopes) y fail fast
      real del host con `WebApplicationFactory<Program>`.

## T-05 · Etapa 2 — Discovery, JWKS y firma

- [ ] `test:` `config/signing-key.json` (RSA 2048) + `JsonSigningKeyStore` + `ITokenSigner`.
- [ ] `test:` `GET /.well-known/openid-configuration` replicando el discovery de referencia
      (`scopes_supported` desde `IScopeStore`, `issuer` con el prefijo `/personafisica/`).
- [ ] `test:` `GET /.well-known/openid-configuration/jwks` (kty RSA, `n`, `e`, `kid`, `alg` RS256, `use` sig).
- [ ] `feat:` firmar un JWT de prueba y verificarlo con `JsonWebTokenHandler` (round-trip en unit tests).

## T-06 · Etapa 3 — Authorization Code

- [ ] `ICodeStore` en memoria + `TimeProvider` (código de un solo uso, con expiración).
- [ ] `GET /connect/authorize`: validación de `client_id`, `redirect_uri`, `response_type`,
      `scope`, `state`, `nonce`, PKCE (`plain` y `S256`) y `prompt`.
- [ ] Pantalla de login mock usando `IUserStore` (contraseña en texto plano) y el `branding` del cliente.
- [ ] Respuesta con `code`, `state`, `iss` (`authorization_response_iss_parameter_supported`).

## T-07 · Etapas 4-7 (pendientes de detalle)

- [ ] Token endpoint con Strategy por grant type (`authorization_code`, `refresh_token`,
      `client_credentials`, `password`, `implicit`).
- [ ] Errores RFC 6749/7009/7662 con `Result<T>` y código HTTP correcto.
- [ ] UserInfo, introspection, revocation, end_session.
- [ ] Device Authorization y CIBA (poll).
- [ ] PAR, `response_modes`, frontchannel/backchannel logout, `check_session_iframe`.

