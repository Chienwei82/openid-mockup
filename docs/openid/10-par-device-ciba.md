# 10 · PAR, Device y CIBA

Tres flujos que no siguen el patrón "authorize → redirect con code" y que el discovery del BCCR anuncia.
El mock los cubre a niveles distintos: PAR **resuelto**, Device y CIBA **simulados**.

## 🟦 Teoría

### PAR — Pushed Authorization Requests (RFC 9126)

En vez de mandar la petición de autorización por la barra de direcciones, el cliente la **empuja** por
back-channel a `/par` y recibe un `request_uri`; luego solo manda ese `request_uri` al authorize. Ventaja:
los parámetros sensibles no viajan en la URL, y el OP valida **antes** de tocar el navegador.

```mermaid
sequenceDiagram
    autonumber
    participant C as Cliente
    participant OP as OP
    C->>OP: POST /par (client auth + params del authorize)
    OP-->>C: 201 { request_uri, expires_in }
    C->>NAV: 302 /authorize?client_id&request_uri
    NAV->>OP: GET /authorize?client_id&request_uri
    OP->>OP: reconstruye y valida la petición guardada
    OP-->>NAV: redirect_uri?code=…  (o error invalid_request_uri)
```

El `request_uri` es de **un solo uso** y caduca.

### Device Authorization Grant (RFC 8628)

Para dispositivos sin navegador (TV, CLI). El dispositivo pide un `device_code` (secreto) y un
`user_code` (legible); el usuario autoriza en **otro** dispositivo; el primero **sondea** el token
endpoint.

```mermaid
sequenceDiagram
    autonumber
    participant D as Dispositivo
    participant OP as OP
    participant U as 👤 (en el móvil/PC)
    D->>OP: POST /deviceauthorization
    OP-->>D: { device_code, user_code, verification_uri, interval }
    D-->>U: muestra el user_code y la URL
    U->>OP: autoriza
    loop hasta que responda
        D->>OP: POST /token grant_type=device_code
        OP-->>D: authorization_pending / slow_down / access_denied / tokens
    end
```

Estados de sondeo (RFC 8628 § 3.5): `authorization_pending`, `slow_down`, `access_denied`,
`expired_token`.

### CIBA — Client-Initiated Backchannel Authentication

Un canal de autenticación iniciado por el **backend** (p. ej. autenticación sin contraseña en un call
center). El cliente obtiene un `auth_req_id` y **sondea** con el mismo ciclo que Device.

## 🟩 Servidor real

El BCCR anuncia los tres endpoints (`/connect/par`, `/connect/deviceauthorization`, `/connect/ciba`),
`require_pushed_authorization_requests: false` (PAR es opcional), y
`backchannel_token_delivery_modes_supported: ["poll"]` (entrega por sondeo, no push).
`backchannel_user_code_parameter_supported: true`.

## 🟨 OidcMock

### PAR — resuelto de verdad

```mermaid
flowchart TD
    P1["POST /connect/par"] --> P2["ClientAuthenticator (basic/post)"]
    P2 --> P3["AuthorizationRequestValidator.Validate"]
    P3 --> P4["PendingAuthorizationRequest (request_uri, 5 min)"]
    P4 --> P5["201 { request_uri, expires_in }"]
    A1["GET /connect/authorize?request_uri"] --> A2["AuthorizationBinder.Bind"]
    A2 --> A3["PushedAuthorizationService.Find(request_uri)"]
    A3 -->|"no existe/caduca"| E["invalid_request_uri"]
    A3 -->|"ok"| A4["vuelve a validar y sigue el flujo normal"]
    A4 --> A5["al conceder → Consume (un solo uso)"]
```

Vive en [`PushedRequests/PushedAuthorizationService.cs`](../../src/OidcMock.Core/PushedRequests/PushedAuthorizationService.cs).
Detalles: se autentica con el **mismo** `ClientAuthenticator`; un `request_uri` desconocido o caducado es
`invalid_request_uri` (no `invalid_request` genérico); la petición reconstruida se **vuelve a validar**
(el código sale de la validación de hoy, no de la del push) y se invalida al conceder.

### Device y CIBA — **simulados**

Servicio en [`DeviceAuthorization/DeviceAuthorizationService.cs`](../../src/OidcMock.Core/DeviceAuthorization/DeviceAuthorizationService.cs)
(`PollAuthorizationService`) y handler de sondeo en
[`Grants/PollGrantHandler.cs`](../../src/OidcMock.Core/Grants/PollGrantHandler.cs).

```mermaid
flowchart TD
    D["POST /connect/deviceauthorization"] --> DD["device_code (10 min), user_code, interval=5s"]
    C["POST /connect/ciba (login_hint)"] --> CC["auth_req_id (5 min)"]
    CC --> AP["si el login_hint existe → Aprueba YA (auto-approve)"]
    subgraph Sondeo ["POST /token grant_type=…"]
        S1{"¿handle inválido/canjeado?"} -->|"sí"| SI["invalid_grant"]
        S1 -->|"no"| S2{"¿caducó?"}
        S2 -->|"sí"| EX["expired_token"]
        S2 -->|"no"| S3{"¿sondea demasiado rápido?"}
        S3 -->|"sí"| SL["slow_down"]
        S3 -->|"no"| S4{"¿usuario aprobó?"}
        S4 -->|"no"| PE["authorization_pending"]
        S4 -->|"denegó"| AD["access_denied"]
        S4 -->|"sí"| TK["tokens"]
    end
```

Qué **no** hace (declarado en [`README.md`](../../README.md)):

- Device: no hay **pantalla de identificación** donde meter el `user_code`. Emite el `device_code`, el
  `user_code` y el `verification_uri` (`urn:oidc-mock:device`), y responde el ciclo RFC 8628, pero
  **nadie autoriza** de verdad, así que el sondeo se queda en `authorization_pending` hasta caducar.
- CIBA: no hay confirmación ni entrega **push**. Si el `login_hint` identifica a alguien del store, la
  petición queda **aprobada de inmediato** (camino feliz para pruebas); si no, se queda pendiente.

> **Para qué sirven estos simulados.** Para que una app que valida el discovery al arrancar no falle y
> para ejercitar el **ciclo de sondeo**. **No** para validar seguridad ni el comportamiento real de esos
> flujos. Ver la tabla de alcance en [`README.md`](../../README.md).

| Endpoint | Estado en el mock |
| --- | --- |
| `POST /connect/par` | ✅ resuelto (RFC 9126) |
| `POST /connect/deviceauthorization` | 🟡 simulado |
| `POST /connect/ciba` | 🟡 simulado |
| `GET /connect/checksession` | 🟡 simulado (página vacía) |

Siguiente: **[11 · Matriz de paridad](11-matriz-de-paridad.md)**.
