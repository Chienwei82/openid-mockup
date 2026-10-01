# Tareas Pendientes

Detalle de lo que falta. El estado de alto nivel vive en [`progress.md`](../progress.md).

## T-01 · Ciclo 1 — `JsonFileLoader` no toca disco sin recarga

- [ ] `test:` test rojo: con `reloadOnChange: false`, tras la primera lectura, borrar el archivo no
      debe lanzar y el store debe seguir devolviendo la configuración cacheada.
- [ ] `fix:` `Load()` devuelve la caché inmediatamente si la recarga está desactivada
      (sin `ReadStamp()` ni lectura del archivo).

## T-02 · Ciclo 2 — Caché del dominio mapeado

- [ ] `test:` test rojo: dos llamadas consecutivas a `List()` devuelven la **misma instancia**
      (referencia estable) y el mapeo no se repite.
- [ ] `refactor:` `JsonFileLoader<TFile, TDomain>` recibe `Func<TFile, TDomain>` y cachea el dominio;
      los tres stores pasan su mapper.

## T-03 · Ciclo 3 — Detección de cambios por contenido

- [ ] `test:` test rojo: editar un valor **del mismo tamaño** y en la misma fracción de segundo
      (p. ej. `#111111` → `#222222`) sin `Thread.Sleep` debe reflejarse en la siguiente consulta.
- [ ] `refactor:` el sello pasa a ser el contenido crudo del archivo (comparación byte a byte);
      se eliminan los `Thread.Sleep(200)` de los tests de recarga.

## T-04 · Ciclo 4 — Simetría de tests y fail fast del host

- [ ] `test:` archivo inexistente para `JsonUserStore` y `JsonScopeStore`.
- [ ] `test:` `reloadOnChange: false` para users y scopes (caché estable).
- [ ] `test:` `WebApplicationFactory<Program>` con configuración inválida ⇒ el arranque falla con
      `ConfigurationException`; y con configuración válida ⇒ arranca y responde.

## T-05 · Etapa 2 — Discovery, JWKS y firma

- [ ] `config/signing-key.json` (RSA 2048) y `JsonSigningKeyStore` + `ITokenSigner`.
- [ ] `GET /.well-known/openid-configuration` replicando el discovery de referencia.
- [ ] `GET /.well-known/openid-configuration/jwks` (kty RSA, n, e, kid, alg RS256, use sig).
- [ ] `iss` con prefijo `/personafisica/` en discovery, id_token y respuestas de autorización.
