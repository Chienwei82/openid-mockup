# Progress — OidcMock

## Etapa 1 — Scaffolding + capa de datos

**Estado: completada.** `dotnet build` en verde (0 warnings, 0 errors) y 34/34 tests en verde.

| Entregable | Estado |
|---|---|
| Solución `.slnx` con los 4 proyectos (`net10.0`, C# 14, nullable, warnings como errores) | ✅ |
| `config/clients.json` (2 clientes) / `users.json` (3 usuarios) / `scopes.json` (8 scopes) | ✅ |
| Core: `Client`, `User`, `ScopeDefinition`, `Branding`, `TokenLifetimes` + `IClientStore`, `IUserStore`, `IScopeStore` | ✅ |
| Host: `JsonClientStore`, `JsonUserStore`, `JsonScopeStore`, mappers, `AddJsonStores()` | ✅ |
| Fail fast con `ConfigurationException` al arrancar | ✅ |
| `reloadOnChange` para editar los JSON sin reiniciar | ✅ |
| Tests: carga válida, inexistente → `null`, JSON mal formado, recarga en caliente, composición | ✅ (34) |
| Memory bank inicializado (`memory-bank/`) | ✅ |

### Cifras

- `OidcMock.UnitTests`: 8 tests.
- `OidcMock.IntegrationTests`: 26 tests.

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
