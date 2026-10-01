# Project Brief — OidcMock

## Qué es

Servidor **OpenID Connect falso** para desarrollo local y offline. Imita el comportamiento
observable de `https://oauth2.bccr.fi.cr/personafisica/` para que las aplicaciones del usuario
puedan apuntar al mock **sin cambios de código**.

- **Stack:** .NET 10 / C# 14, Minimal APIs, sin base de datos.
- **Datos:** archivos JSON en `config/` (clients, users, scopes, signing key).
- **Estado:** tokens, códigos de autorización y refresh tokens viven **en memoria**.

## Objetivo por etapas

| Etapa | Alcance | Estado |
|---|---|---|
| 1 | Scaffolding + capa de datos (JSON stores) | ✅ completada |
| 2 | Discovery + JWKS + firma RS256 | pendiente |
| 3 | Authorization Code (+ PKCE), login/consentimiento mock | pendiente |
| 4 | Token endpoint: authorization_code, refresh_token, client_credentials, password, implicit | pendiente |
| 5 | UserInfo, introspection, revocation, end_session | pendiente |
| 6 | Device Authorization y CIBA (poll) | pendiente |
| 7 | PAR, response_modes, frontchannel/backchannel logout | pendiente |

## Reglas innegociables (de `AGENTS.md`)

1. **TDD estricto:** test rojo → implementación mínima → refactor. Un commit por ciclo.
2. **Dependencias mínimas:** producción solo framework + `Microsoft.IdentityModel.JsonWebTokens`;
   tests solo `xunit.v3` + `Microsoft.AspNetCore.Mvc.Testing`. Cualquier otro paquete se
   **propone y justifica** antes de usarse.
3. **Sin base de datos.** Configuración en JSON, estado en memoria.
4. **Estructura:** `src/OidcMock.Core` (dominio, **sin** referencia a ASP.NET),
   `src/OidcMock.Host` (Minimal APIs, stores JSON, crypto), `tests/OidcMock.UnitTests`,
   `tests/OidcMock.IntegrationTests`. Solución `.slnx`.
5. **Clean Code:** nombres que revelan intención, funciones cortas, una responsabilidad por clase,
   sin comentarios obvios, sin números/cadenas mágicas, `nullable` habilitado, warnings como errores.
6. **Patrones:** Strategy para grant types, repositorios de solo lectura, Options para configuración,
   `Result<T>` para errores de negocio, `TimeProvider` para el reloj, DI por constructor.
7. **Errores de protocolo** según RFC 6749/6750/7009/7662 con el código HTTP correcto.
8. **Cierre de cada prompt:** `dotnet build` y tests en verde + resumen de 5 líneas.

## Criterio de éxito

Una app real (SPA, backend, móvil) cambia **solo** su `Authority`/`MetadataAddress` a
`http://localhost:5000/personafisica/` y completa el flujo de login,Silent,Refresh y UserInfo
sin tocar una línea de su código de autenticación.
