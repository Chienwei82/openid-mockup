"""Markup interno de cada escena del explicador.

Contrato HyperFrames respetado aquí:
  * sin `<br>` en texto de cuerpo,
  * sin atributos `id` (evita colisiones al ensamblar),
  * todo elemento animado lleva `data-anim` con su variante de entrada
    (`rise`, `pop`, `slide` o `fade`),
  * todo elemento que la narración menciona lleva `data-hl="<clave>"`; la
    clave se anuda con las pistas `hl` de scripts/narration.json para que el
    build lo resalte justo cuando se pronuncia su frase.
"""

from __future__ import annotations

TOKEN_LIFETIMES = (
    ("authorization_code", "5 min", "opaco, un solo uso", "row-code"),
    ("access_token", "30 min", "JWT RS256, aud = client_id", "row-access"),
    ("id_token", "30 min", "solo con scope openid", "row-id"),
    ("refresh_token", "8 h", "rota, requiere offline_access", "row-refresh"),
)

ERROR_CODES = (
    ("invalid_request", "400", "falta o estorba un parámetro"),
    ("invalid_client", "401", "credenciales de cliente inválidas"),
    ("invalid_grant", "400", "code, refresh o PKCE no válidos"),
    ("unsupported_grant_type", "400", "grant desconocido"),
    ("invalid_scope", "400", "scope no permitido"),
    ("invalid_token", "401", "reto Bearer en /userinfo"),
)


def hl_attr(key: str | None) -> str:
    return f' data-hl="{key}"' if key else ""


def with_keys(items: tuple[str | tuple[str, str], ...]) -> list[tuple[str | None, str]]:
    return [item if isinstance(item, tuple) else (None, item) for item in items]


def head(kicker: str, title: str) -> str:
    return (
        '<header class="scene__head">'
        f'<span class="kicker" data-anim="pop">{kicker}</span>'
        f'<h2 class="headline" data-anim="rise">{title}</h2>'
        "</header>"
    )


def card(title: str, body: str, hl: str | None = None) -> str:
    return (
        f'<article class="card" data-anim="rise"{hl_attr(hl)}>'
        f'<h3 class="card__title">{title}</h3>{body}</article>'
    )


def bullet_list(items: tuple[str | tuple[str, str], ...]) -> str:
    return (
        '<ul class="list">'
        + "".join(f"<li{hl_attr(key)}>{text}</li>" for key, text in with_keys(items))
        + "</ul>"
    )


def chips(items: tuple[str | tuple[str, str], ...], variant: str = "", hl: str | None = None) -> str:
    return (
        f'<div class="chiprow" data-anim="fade"{hl_attr(hl)}>'
        + "".join(
            f'<span class="chip{variant}"{hl_attr(key)}>{text}</span>' for key, text in with_keys(items)
        )
        + "</div>"
    )


def steps(items: tuple[str | tuple[str, str], ...]) -> str:
    return (
        '<ol class="steps">'
        + "".join(
            f'<li data-anim="slide"{hl_attr(key)}><span class="steps__n">{index}</span>{text}</li>'
            for index, (key, text) in enumerate(with_keys(items), start=1)
        )
        + "</ol>"
    )


SCENE_BODY: dict[str, str] = {}

# ─────────────────────────────────────────────────────────────────────────────
SCENE_BODY["scene-01-title"] = (
    '<section class="scene scene--center">'
    '<span class="kicker" data-anim="pop">Introducción</span>'
    '<h1 class="headline headline--xl">'
    '<span class="headline__line" data-anim="rise" data-hl="title-oidc">OpenID Connect</span>'
    '<span class="headline__line headline__line--accent" data-anim="rise">Explicado</span>'
    "</h1>"
    '<p class="lede" data-anim="fade">Del protocolo al mock para desarrollo local, con OidcMock</p>'
    + chips((("title-mock", "OidcMock"), "1920 × 1080", "Narración en español"))
    + "</section>"
)

SCENE_BODY["scene-02-actores"] = (
    '<section class="scene">'
    + head("Actores", "Actores y Tokens")
    + '<div class="grid grid--2">'
    + card(
        "Cuatro roles (OAuth 2.0)",
        bullet_list(
            (
                ("role-owner", "<strong>Resource Owner</strong> — el usuario"),
                ("role-client", "<strong>Client</strong> — la aplicación"),
                ("role-as", "<strong>Authorization Server</strong> — emite los tokens"),
                ("role-rs", "<strong>Resource Server</strong> — sirve la API protegida"),
            )
        ),
    )
    + card(
        "Cuatro tokens",
        bullet_list(
            (
                ('tok-code', '<span class="tok tok--code">code</span> Authorization Code — opaco, un solo uso'),
                ('tok-access', '<span class="tok tok--access">access</span> Access Token — JWT para la API'),
                ('tok-id', '<span class="tok tok--id">id</span> ID Token — JWT de identidad'),
                ('tok-refresh', '<span class="tok tok--refresh">refresh</span> Refresh Token — vida larga'),
            )
        ),
    )
    + "</div></section>"
)

SCENE_BODY["scene-03-discovery"] = (
    '<section class="scene">'
    + head("Discovery", "Discovery y JWKS")
    + '<div class="grid grid--2">'
    + card(
        "Discovery Document",
        '<p class="mono">GET /.well-known/openid-configuration</p>'
        + bullet_list(
            (
                "issuer — comparado literalmente por el cliente",
                "authorization · token · userinfo · introspect · revocation",
                "scopes_supported y grant_types_supported",
            )
        ),
        hl="card-discovery",
    )
    + card(
        "JWKS",
        '<p class="mono">GET /.well-known/openid-configuration/jwks</p>'
        + bullet_list(
            (
                "JSON Web Key Set — claves públicas del emisor",
                "kid identifica qué clave firmó el JWT",
                "Algoritmo de firma: RS256",
            )
        ),
        hl="card-jwks",
    )
    + "</div></section>"
)

SCENE_BODY["scene-04-pkce"] = (
    '<section class="scene">'
    + head("Flujo principal", "Authorization Code + PKCE")
    + steps(
        (
            ("step-verifier", "El cliente genera <code>code_verifier</code> y <code>code_challenge</code>"),
            ("step-authorize", "Redirige a <code>/connect/authorize</code> con el challenge y <code>state</code>"),
            "El OP autentica al usuario y pide consentimiento",
            "Vuelve al cliente con el <code>authorization_code</code>",
            ("step-canje", "El cliente canjea en <code>/connect/token</code> con el <code>code_verifier</code>"),
            ("step-compara", "Comparación en tiempo constante · S256 recomendado sobre plain"),
        )
    )
    + "</section>"
)

SCENE_BODY["scene-05-idtoken"] = (
    '<section class="scene">'
    + head("Claims", "ID Token y Claims")
    + '<div class="grid grid--2">'
    + card(
        "Claims del JWT",
        '<p class="card__label">Obligatorios</p>'
        + chips(
            (
                ("chip-iss", "iss"),
                ("chip-sub", "sub"),
                ("chip-aud", "aud"),
                ("chip-exp", "exp"),
                ("chip-iat", "iat"),
                ("chip-nonce", "nonce"),
            ),
            " chip--accent",
        )
        + '<p class="card__label">Binding</p>'
        + chips(("at_hash", "c_hash", "sid"), hl="chiprow-binding"),
    )
    + card(
        "Proyección por scope",
        bullet_list(
            (
                "<strong>openid</strong> → sub",
                "<strong>profile</strong> → name, given_name, family_name, locale",
                "<strong>email</strong> → email, email_verified",
                "<strong>address</strong> → address",
            )
        ),
        hl="card-scope",
    )
    + "</div></section>"
)

SCENE_BODY["scene-06-grants"] = (
    '<section class="scene">'
    + head("Grants", "Token Endpoint y Grant Types")
    + '<p class="mono" data-anim="fade" data-hl="line-token">POST /connect/token</p>'
    + '<div class="statrow">'
    + "".join(
        f'<div class="stat" data-anim="pop"{hl_attr(hl)}><span class="stat__num">{value}</span>'
        f'<span class="stat__label">{label}</span></div>'
        for value, label, hl in (
            ("7", "grant types anunciados", "stat-grants"),
            ("6", "endpoints de protocolo", None),
            ("1", "handler por grant", "stat-handler"),
        )
    )
    + "</div>"
    + chips(
        (
            ("chip-authorization-code", "authorization_code"),
            ("chip-refresh-token", "refresh_token"),
            ("chip-client-credentials", "client_credentials"),
            ("chip-password", "password"),
            ("chip-device-code", "device_code"),
            ("chip-ciba", "ciba"),
            ("chip-implicit", "implicit"),
        )
    )
    + "</section>"
)

SCENE_BODY["scene-07-userinfo"] = (
    '<section class="scene">'
    + head("Gestión", "UserInfo, Introspection y Revocation")
    + '<div class="grid grid--3">'
    + card(
        "UserInfo",
        '<p class="mono">GET /connect/userinfo</p>'
        + bullet_list(
            (
                "Devuelve los claims del usuario",
                "Requiere <code>Authorization: Bearer</code>",
                "Reto <code>WWW-Authenticate</code> si el token no vale",
            )
        ),
        hl="card-userinfo",
    )
    + card(
        "Introspection",
        '<p class="mono">POST /connect/introspect</p>'
        + bullet_list(
            (
                "Responde <code>active: true</code> o <code>false</code>",
                "Nunca revela si el token existía",
                "RFC 7662, autenticado como cliente",
            )
        ),
        hl="card-introspection",
    )
    + card(
        "Revocation",
        '<p class="mono">POST /connect/revocation</p>'
        + bullet_list(
            (
                "Access token: registra su <code>jti</code>",
                "Refresh token: revoca la familia completa",
                "RFC 7009, idempotente",
            )
        ),
        hl="card-revocation",
    )
    + "</div></section>"
)

SCENE_BODY["scene-08-sesion"] = (
    '<section class="scene">'
    + head("Sesión", "Sesión y Logout")
    + '<div class="grid grid--3">'
    + card(
        "Sesión del OP",
        bullet_list(("Habilita el SSO", "Cookie del navegador", "Vida por defecto 8 h")),
        hl="card-op",
    )
    + card(
        "Sesión del cliente",
        bullet_list(("Cookie propia de la aplicación", "Independiente del OP", "Se cierra con el logout")),
        hl="card-cliente",
    )
    + card(
        "sid",
        bullet_list(("Conecta el id_token con la sesión", "Viaja también en el logout", "Permite front-channel logout")),
        hl="card-sid",
    )
    + "</div>"
    + '<div class="grid grid--2 grid--tight">'
    + card(
        "prompt",
        chips(("none", "login", "consent", "select_account")),
        hl="card-prompt",
    )
    + card(
        "RP-Initiated Logout",
        '<p class="mono">/connect/endsession · id_token_hint · post_logout_redirect_uri</p>',
        hl="card-logout",
    )
    + "</div></section>"
)

SCENE_BODY["scene-09-avanzados"] = (
    '<section class="scene">'
    + head("Avanzado", "Flujos Avanzados")
    + '<div class="grid grid--3">'
    + card(
        'PAR <span class="badge badge--ok">implementado</span>',
        bullet_list(
            (
                "RFC 9126, un solo uso",
                "Envía los parámetros por back-channel",
                "Devuelve <code>request_uri</code> al cliente",
            )
        ),
        hl="card-par",
    )
    + card(
        'Híbrido <span class="badge badge--ok">implementado</span>',
        bullet_list(
            (
                "<code>code id_token</code> en el fragmento",
                "Incluye <code>c_hash</code> y <code>session_state</code>",
                "Pensado para el sistema interno",
            )
        ),
        hl="card-hibrido",
    )
    + card(
        'Device y CIBA <span class="badge badge--warn">simulados</span>',
        bullet_list(
            (
                "Ciclo de sondeo, sin pantalla ni push",
                "<code>authorization_pending</code>, <code>slow_down</code>",
                "Validan el discovery anunciado",
            )
        ),
        hl="card-device",
    )
    + "</div>"
    + '<p class="note" data-anim="fade">Implicit (<code>id_token token</code>) se anuncia pero no se responde: devuelve <code>unsupported_response_type</code>.</p>'
    + "</section>"
)

SCENE_BODY["scene-10-matriz"] = (
    '<section class="scene">'
    + head("Resumen", "Matriz de Paridad")
    + '<div class="grid grid--matrix">'
    + '<table class="table" data-anim="rise"><thead><tr><th>Token</th><th>Vida</th><th>Notas</th></tr></thead><tbody>'
    + "".join(
        f'<tr{hl_attr(hl)}><td class="mono">{name}</td><td>{life}</td><td>{note}</td></tr>'
        for name, life, note, hl in TOKEN_LIFETIMES
    )
    + "</tbody></table>"
    + card(
        "Errores de protocolo",
        "".join(
            f'<p class="errow"><span class="chip chip--accent">{code}</span>'
            f'<span class="errow__http">{http}</span>{label}</p>'
            for code, http, label in ERROR_CODES
        ),
        hl="card-errores",
    )
    + "</div>"
    + '<p class="note" data-anim="fade">El usuario denegó: <code>access_denied</code> · <code>request_uri</code> caducado: <code>invalid_request_uri</code>.</p>'
    + "</section>"
)

SCENE_BODY["scene-11-arranque"] = (
    '<section class="scene">'
    + head("Práctico", "Arrancar OidcMock")
    + '<div class="grid grid--2">'
    + card(
        "Comandos",
        f'<pre class="code"{hl_attr("cmd-run")}><code>dotnet run --project src/OidcMock.Host</code></pre>'
        f'<pre class="code"{hl_attr("cmd-issuer")}><code>--OidcMock:Issuer=https://localhost:5443/personafisica/</code></pre>'
        '<pre class="code"><code>--OidcMock:Serving:UseHttps=false --OidcMock:Serving:AllowHttp=true</code></pre>',
    )
    + card(
        "Clientes de ejemplo (config/)",
        bullet_list(
            (
                '<span class="tok tok--code">público</span> web-app-spa — PKCE, sin secreto',
                '<span class="tok tok--access">confidencial</span> web-app-confidencial',
                '<span class="tok tok--refresh">servicio</span> backend-service — client_credentials',
            )
        )
        + '<p class="card__label">Opciones clave</p>'
        + chips(("PathBase /personafisica", "ConfigDirectory ./config", "ReloadOnChange true")),
        hl="card-clientes",
    )
    + "</div></section>"
)

SCENE_BODY["scene-12-outro"] = (
    '<section class="scene scene--center">'
    '<span class="kicker" data-anim="pop">Cierre</span>'
    '<h2 class="headline" data-anim="rise">Desarrolla sin dependencias externas</h2>'
    '<p class="lede" data-anim="fade" data-hl="lede">OidcMock replica el comportamiento del servidor de referencia para desarrollo local: '
    'configuración JSON, estado en memoria y ningún artefacto fuera de '
    '<code data-hl="code-localhost">localhost</code>.</p>'
    + chips(("docs/openid/", "scripts/test.sh", "OidcMock v1.0"))
    + "</section>"
)

# ─────────────────────────────────────────────────────────────────────────────
