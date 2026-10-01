# Progress — OidcMock

## Etapa 1 — Scaffolding + capa de datos

**Estado: completada.** `dotnet build` en verde (0 warnings, 0 errors) y **43/43 tests** en verde
(8 unit + 35 integration). El host arranca con `dotnet run` y lee `config/` del repositorio.

| Entregable | Estado |
|---|---|
| Solución `.slnx` con los 4 proyectos (`net10.0`, C# 14, nullable, warnings como errores) | ✅ |
| `config/clients.json` (2 clientes) / `users.json` (3 usuarios) / `scopes.json` (8 scopes) | ✅ |
| Core: `Client`, `User`, `ScopeDefinition`, `Branding`, `TokenLifetimes` + `IClientStore`, `IUserStore`, `IScopeStore` | ✅ |
| Host: `JsonClientStore`, `JsonUserStore`, `JsonScopeStore`, mappers, `AddJsonStores()` | ✅ |
| Fail fast con `ConfigurationException` al arrancar (probado con `WebApplicationFactory<Program>`) | ✅ |
| `reloadOnChange` por comparación de contenido, sin `Thread.Sleep` en los tests | ✅ |
| Tests: carga válida, inexistente → `null`, JSON mal formado, archivo ausente, recarga en caliente, caché sin recarga, arranque del host | ✅ (43) |
| Memory bank inicializado (`memory-bank/`) | ✅ |

### Cifras

- `OidcMock.UnitTests`: 8 tests.
- `OidcMock.IntegrationTests`: 35 tests.

### Commits de esta sesión

```
docs:      inicializa el memory bank del proyecto
test/fix:  la caché se sirve aunque el archivo desaparezca con reloadOnChange=false
test/refactor: el loader cachea el dominio ya mapeado (JsonFileLoader<TFile, TDomain>)
test/refactor: detección de cambios comparando el contenido (fuera el sello de disco)
test/feat: seam OidcMock:ConfigDirectory + fail fast real del host
```

## Endpoints / funcionalidades

| Ítem | Estado |
|---|---|
| Discovery + JWKS + firma RS256 | ⬜ pendiente (etapa 2) |
| Authorization Code + PKCE + pantallas de login | ⬜ pendiente (etapa 3) |
| Token endpoint (code, refresh, client_credentials, password, implicit) | ⬜ pendiente (etapa 4) |
| UserInfo, introspection, revocation, end_session | ⬜ pendiente (etapa 5) |
| Device Authorization y CIBA | ⬜ pendiente (etapa 6) |
| PAR, response_modes, logout front/backchannel | ⬜ pendiente (etapa 7) |

## Deuda técnica conocida

- `dotnet test` a nivel de solución aborta en este entorno (`Internal CLR error 0x80131506`);
  ejecutar por proyecto o con los ejecutables de los runners (ver D-011).
- Sin tests de expiración de tokens todavía; se resolverá con `TimeProvider` en la etapa 4.
