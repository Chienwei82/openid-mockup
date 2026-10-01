# System Design — OidcMock

## Proyectos

```
OidcMock.slnx
├── src/OidcMock.Core          Dominio + casos de uso. SIN referencia a ASP.NET.
│   ├── Clients/                Client, Branding, TokenLifetimes, IClientStore
│   ├── Users/                  User, IUserStore, ClaimNotFoundException
│   ├── Scopes/                 ScopeDefinition, IScopeStore
│   └── Configuration/          JsonStoreOptions, ConfigurationFiles,
│                               ConfigurationException, IConfigurationValidator
├── src/OidcMock.Host          Minimal APIs, stores JSON, crypto (futuro).
│   ├── Stores/                 JsonClientStore, JsonUserStore, JsonScopeStore
│   │   ├── Configuration/      DTOs del formato de los JSON (ClientFile, UserFile, ScopeFile)
│   │   └── (infra)             JsonFileLoader<TFile,TDomain>, mappers, JsonConfiguration
│   ├── HostConfigDirectory.cs  Localiza config/ en run y en publish
│   ├── ServiceCollectionExtensions.cs  AddJsonStores()
│   └── Program.cs              Composición + fail fast
├── tests/OidcMock.UnitTests        Solo Core (dominio puro)
└── tests/OidcMock.IntegrationTests Stores JSON + composición (referencia a Host)
```

`OidcMock.UnitTests` **no** referencia a `OidcMock.Host`; por eso los tests de los stores viven
en `IntegrationTests` (decisión D-002 en [`docs/decisions.md`](../docs/decisions.md)).

## Capa de datos (etapa 1, implementada)

```
config/*.json
   │  JsonFileLoader<TFile, TDomain>   caché en memoria, recarga si cambia el contenido
   ▼
DTO  (ClientFile / UserFile / ScopeFile)  internal, modela el JSON con JsonPropertyName
   │  ClientMapper / UserMapper / ScopeMapper   static, lanzan ConfigurationException
   ▼
Dominio  (Client / User / ScopeDefinition)      records inmutables en Core
   ▲
I*Store  (solo lectura)  ← consumidores (casos de uso futuros)
```

- `JsonFileLoader<TFile, TDomain>` recibe la función de proyección al construirse, cachea el
  **dominio ya mapeado** y compara el contenido crudo byte a byte para decidir si relee.
- `reloadOnChange: false` ⇒ se lee **una** vez y se devuelve la caché sin tocar disco.
- Concurrencia: todo el `Load()` bajo `Lock` (`System.Threading.Lock`, .NET 9+).
- `AddJsonStores(options | configDirectory, reloadOnChange)` registra los tres stores como
  **singleton**, más `JsonStoreOptions` y `TimeProvider.System`.
- `IConfigurationValidator` fuerza la lectura de los tres archivos; `Program.cs` lo invoca
  **antes** de `app.Run()` ⇒ fail fast con `ConfigurationException` (mensaje incluye archivo y motivo).

## Formato de los JSON

Raíz con clave: `{"clients": [...]}`, `{"users": [...]}`, `{"scopes": [...]}`, para poder agregar
metadatos futuros sin romper el formato. Campos en `snake_case`; vigencias como `TimeSpan`
(`"00:30:00"`). `User.Claims` es `IReadOnlyDictionary<string, JsonElement>`: nombres libres y valores
que conservan el tipo JSON (bool, número, arreglo) para serializarlos después sin pérdida.

## Rutas previstas (todas bajo `/personafisica/`)

| Ruta | Método | Etapa |
|---|---|---|
| `/.well-known/openid-configuration` | GET | 2 |
| `/.well-known/openid-configuration/jwks` | GET | 2 |
| `/connect/authorize` | GET/POST | 3 |
| `/connect/token` | POST | 4 |
| `/connect/userinfo` | GET/POST | 5 |
| `/connect/endsession` | GET/POST | 5 |
| `/connect/checksession` | GET (iframe) | 7 |
| `/connect/revocation` | POST | 5 |
| `/connect/introspect` | POST | 5 |
| `/connect/deviceauthorization` | POST | 6 |
| `/connect/ciba` | POST | 6 |
| `/connect/par` | POST | 7 |

## Configuración del host

- `HostConfigDirectory.Resolve` busca, en orden: `<ContentRoot>/config`,
  `<ContentRoot>/../../config` (el `config/` del repo, para editarlo con `dotnet run`) y
  `<AppContext.BaseDirectory>/config` (publicación).
- `OidcMock:ConfigDirectory` y `OidcMock:ReloadOnChange` sobrescriben la resolución; sin
  `ConfigDirectory` gana `HostConfigDirectory`. Lo usan las pruebas de arranque y los despliegues.
- El `.csproj` del Host copia `config/*.json` a la salida (`PreserveNewest`).
- `InternalsVisibleTo("OidcMock.IntegrationTests")` permite probar los tipos internos de mapeo.

## Estado en memoria (futuro)

`ICodeStore`, `IRefreshTokenStore`, `ITokenSigner`, `ISessionStore` con implementación en memoria
y `TimeProvider` inyectado, para que los tests puedan simular expiraciones sin `Thread.Sleep`.
