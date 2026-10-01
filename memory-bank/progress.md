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

### Commits de la etapa 1

| Commit | Mensaje |
|---|---|
| `b343700` | prompt 1 (scaffolding + capa de datos, en un solo commit) |
| `fd6bc6f` | `docs:` inicializa el memory bank del proyecto |
| `96a27f2` | `test:` la caché se sirve aunque el archivo desaparezca con `reloadOnChange=false` |
| `d07bc3c` | `fix:` no releer el archivo cuando la recarga en caliente está desactivada |
| `749a9f7` | `test:` el dominio cacheado debe reutilizarse entre consultas |
| `c56cbde` | `refactor:` el loader cachea el dominio ya mapeado |
| `91ea840` | `test:` detectar cambios que conservan el sello de disco |
| `e671edd` | `refactor:` detectar cambios comparando el contenido |
| `e0db41c` | `test:` el arranque del host debe fallar con configuración inválida |
| `38ac8c2` | `feat:` seam `OidcMock:ConfigDirectory` y `OidcMock:ReloadOnChange` |
| `8b84dba` | `docs:` actualiza decisiones y memory bank |

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
