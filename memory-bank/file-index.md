# File Index — OidcMock

Solo archivos con significado para el proyecto; se omiten `bin/`, `obj/` y `.git/`.

## Raíz

| Ruta | Rol |
|---|---|
| `AGENTS.md` | Reglas innegociables del proyecto + discovery de referencia del BCCR. Fuente de verdad del "qué". |
| `OidcMock.slnx` | Solución (`src/` y `tests/` en carpetas virtuales). |
| `Directory.Build.props` | net10.0, C# 14, nullable, warnings como errores, análisis, estilo. |
| `Directory.Packages.props` | Versiones centralizadas de los paquetes. |
| `.editorconfig` | Convenciones de estilo y nomenclatura (sistema de archivos binario). |
| `docs/decisions.md` | Histórico de decisiones de diseño (ADRs con contexto). |
| `memory-bank/` | Este contexto persistente entre sesiones. |

## `config/` — datos del mock (editables en caliente)

| Ruta | Contenido |
|---|---|
| `config/clients.json` | 2 clientes de ejemplo: `web-app-spa` (público, PKCE) y `backend-service` (secreto, client_credentials). |
| `config/users.json` | 3 usuarios de ejemplo con `claims` libres: `jperez`, `empresa-demo`, `admin`. |
| `config/scopes.json` | 8 scopes con los claims que expone cada uno. |
| `config/signing-key.json` | *(pendiente, etapa 2)* clave RSA de firma. |

## `src/OidcMock.Core` — dominio (sin ASP.NET)

| Ruta | Rol |
|---|---|
| `Clients/Client.cs` | Record inmutable del cliente + `AllowsGrantType/RedirectUri/PostLogoutRedirectUri/Scope`. |
| `Clients/Branding.cs` | `display_name`, `logo_url`, `primary_color` de las pantallas de login. |
| `Clients/TokenLifetimes.cs` | Vigencias de access, id, refresh y authorization code. |
| `Clients/IClientStore.cs` | `Find(clientId)`, `List()`. |
| `Users/User.cs` | Record con `Claims` libre + `GetClaim/TryGetClaim/HasClaim`. |
| `Users/IUserStore.cs` | `FindByUserName`, `FindBySubject`, `List()`. |
| `Users/ClaimNotFoundException.cs` | Claim pedido y no configurado. |
| `Scopes/ScopeDefinition.cs` | Nombre del scope + claims que expone. |
| `Scopes/IScopeStore.cs` | `Find(name)`, `List()`. |
| `Configuration/JsonStoreOptions.cs` | Options: `ConfigDirectory`, `ReloadOnChange`. |
| `Configuration/ConfigurationFiles.cs` | Nombres de archivo (`clients.json`, `users.json`, `scopes.json`). |
| `Configuration/ConfigurationException.cs` | Error de configuración con `FileName` y `Reason`. |
| `Configuration/IConfigurationValidator.cs` | Fail fast en el arranque. |

## `src/OidcMock.Host` — entrega

| Ruta | Rol |
|---|---|
| `Program.cs` | Composición, `AddJsonStores`, validación fail fast, `app.Run()`. |
| `ServiceCollectionExtensions.cs` | `AddJsonStores(...)`: singletons de los tres stores + `TimeProvider.System`. |
| `HostConfigDirectory.cs` | Resuelve el directorio `config/` (run / repo / publish). |
| `Stores/JsonFileLoader.cs` | Caché con recarga por comparación de contenido + `Lock`. |
| `Stores/JsonClientStore.cs` | `IClientStore` sobre `clients.json`. |
| `Stores/JsonUserStore.cs` | `IUserStore` sobre `users.json`. |
| `Stores/JsonScopeStore.cs` | `IScopeStore` sobre `scopes.json`. |
| `Stores/ClientMapper.cs` | DTO → `Client`, valida `client_id` y `token_lifetimes`. |
| `Stores/UserMapper.cs` | DTO → `User`, valida `sub` y `username`. |
| `Stores/ScopeMapper.cs` | DTO → `ScopeDefinition`, valida `name`. |
| `Stores/JsonConfiguration.cs` | `JsonSerializerOptions` común (comentarios y comas finales permitidos). |
| `Stores/JsonConfigurationValidator.cs` | Fuerza la lectura de los tres archivos. |
| `Stores/Configuration/ClientFile.cs` | Forma de `clients.json` (incluye `TokenLifetimesEntry`, `BrandingEntry`). |
| `Stores/Configuration/UserFile.cs` | Forma de `users.json` (`claims` como `Dictionary<string, JsonElement>`). |
| `Stores/Configuration/ScopeFile.cs` | Forma de `scopes.json`. |

## Pruebas

| Ruta | Rol |
|---|---|
| `tests/OidcMock.UnitTests/Clients/ClientTests.cs` | Predicados del record `Client`. |
| `tests/OidcMock.UnitTests/Scopes/ScopeDefinitionTests.cs` | Inmutabilidad de `ScopeDefinition`. |
| `tests/OidcMock.IntegrationTests/Stores/JsonClientStoreTests.cs` | Carga, `null`, JSON roto, archivo ausente, hot reload, caché sin reload, `client_id` vacío. |
| `tests/OidcMock.IntegrationTests/Stores/JsonUserStoreTests.cs` | Carga, claims libres y no textuales, búsqueda por `sub`, `null`, JSON roto, hot reload. |
| `tests/OidcMock.IntegrationTests/Stores/JsonScopeStoreTests.cs` | Carga, `null`, JSON roto, hot reload. |
| `tests/OidcMock.IntegrationTests/Composition/AddJsonStoresTests.cs` | Registro en DI, lectura de la config de ejemplo, fail fast. |
| `tests/OidcMock.IntegrationTests/TempConfigDirectory.cs` | Directorio temporal por test para mutar JSON. |
| `tests/OidcMock.IntegrationTests/RepositoryLayout.cs` | Localiza el `config/` del repositorio desde los tests. |
