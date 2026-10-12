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


def where_you_are(active: str) -> str:
    crumbs = (
        ("1", "Discovery"),
        ("2-3", "Authorize"),
        ("4", "Canje"),
        ("5-6", "Uso"),
    )
    return (
        '<p class="where" data-anim="fade">'
        '<span class="where__label">Estás aquí</span>'
        + "".join(
            f'<span class="where__crumb{" where__crumb--on" if label == active else ""}">'
            f"<span class=\"where__n\">{number}</span>{label}</span>"
            for number, label in crumbs
        )
        + "</p>"
    )


def lanes(
    lanes: tuple[tuple[str, tuple[str | tuple[str, str], ...]], ...],
    active_lane: int,
) -> str:
    return (
        '<div class="lanes">'
        + "".join(
            f'<div class="lane{" lane--off" if index != active_lane else ""}" data-anim="rise">'
            f'<p class="lane__name">{name}</p>'
            + "".join(
                f'<p class="hop{"" if index == active_lane else " hop--off"}"{hl_attr(key)}>'
                f'<span class="hop__n">{number}</span>{text}</p>'
                for number, (key, text) in enumerate(with_keys(hops), start=1)
            )
            + "</div>"
            for index, (name, hops) in enumerate(lanes)
        )
        + "</div>"
    )



# ─────────────────────────────────────────────────────────────────────────────
# El mapa fijo: el flujo completo en seis paradas, con sus seis claves de voz.
FLOW_STOPS: tuple[tuple[str, str], ...] = (
    ("flujo-discovery", "El cliente descubre los endpoints"),
    ("flujo-authorize", "Redirige al navegador al authorize"),
    ("flujo-aprueba", "El usuario aprueba y vuelve con el código"),
    ("flujo-canje", "El cliente canjea el código por tokens"),
    ("flujo-userinfo", "Lee la identidad en userinfo"),
    ("flujo-api", "Llama a la API con el access token"),
)

MAP_ACTORS: tuple[str, ...] = ("Cliente", "Navegador", "OP · OidcMock", "API")


def flow_map(active: int | None = None) -> str:
    stops = "".join(
        f'<li class="map__stop{" map__stop--on" if number == active else ""}"'
        f' data-anim="slide"{hl_attr(key)}>'
        f'<span class="map__n">{number}</span>{label}</li>'
        for number, (key, label) in enumerate(FLOW_STOPS, start=1)
    )
    actors = "".join(
        f'<span class="map__actor" data-anim="pop">{actor}</span>' for actor in MAP_ACTORS
    )
    return f'<div class="map" data-anim="fade"><p class="map__actors">{actors}</p><ol class="map__stops">{stops}</ol></div>'


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

SCENE_BODY["scene-03-flujo"] = (
    '<section class="scene">'
    + head("El mapa", "El Flujo Completo, en Seis Pasos")
    + flow_map()
    + '<p class="note" data-anim="fade">Memoriza este mapa: cada escena siguiente amplía una de sus paradas.</p>'
    + "</section>"
)

SCENE_BODY["scene-04-discovery"] = (
    '<section class="scene">'
    + head("Paso 1 del mapa", "Discovery y JWKS")
    + where_you_are("Discovery")
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

SCENE_BODY["scene-05-pkce"] = (
    '<section class="scene">'
    + head("Pasos 2 y 3 del mapa", "Authorization Code + PKCE")
    + where_you_are("Authorize")
    + lanes(
        (
            (
                "Cliente",
                (
                    ("step-verifier", "Genera <code>code_verifier</code> y <code>code_challenge</code>"),
                    ("step-authorize", "Redirige a <code>/connect/authorize</code> con challenge y <code>state</code>"),
                ),
            ),
            (
                "OP · OidcMock",
                (
                    ("step-aprueba", "Autentica al usuario y pide consentimiento"),
                    ("step-canje", "Devuelve el <code>authorization_code</code> al redirect_uri"),
                    ("step-compara", "Al canjear, compara en tiempo constante · S256 sobre plain"),
                ),
            ),
        ),
        1,
    )
    + "</section>"
)

SCENE_BODY["scene-06-idtoken"] = (
    '<section class="scene">'
    + head("Paso 4 del mapa", "ID Token y Claims")
    + where_you_are("Canje")
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

SCENE_BODY["scene-07-canje"] = (
    '<section class="scene">'
    + head("Paso 4 del mapa", "El Canje en el Token Endpoint")
    + where_you_are("Canje")
    + steps(
        (
            ("paso-envia", "El cliente envía <code>code</code>, <code>redirect_uri</code> y <code>code_verifier</code>"),
            ("paso-valida", "El servidor valida el código: existe, es de ese cliente y no se usó"),
            ("paso-comprueba", "Comprueba el verifier contra el challenge guardado"),
            ("paso-responde", "Responde <code>access_token</code> + <code>id_token</code> + <code>refresh_token</code>"),
            ("paso-repite", "Repetir el código es <code>invalid_grant</code>: ya no vale"),
        )
    )
    + "</section>"
)

SCENE_BODY["scene-08-grants"] = (
    '<section class="scene">'
    + head("Otros caminos", "Otros Grants del Token Endpoint")
    + '<p class="mono" data-anim="fade">POST /connect/token — un handler por grant</p>'
    + '<div class="grid grid--2">'
    + card(
        "Con usuario",
        bullet_list(
            (
                ("card-refresh", "<strong>refresh_token</strong> — pide tokens nuevos sin login, rota en cada uso"),
                ("card-password", "<strong>password</strong> — valida usuario y contraseña directamente"),
            )
        ),
    )
    + card(
        "Sin usuario",
        bullet_list(
            (
                ("card-client", "<strong>client_credentials</strong> — tokens de servicio, sin usuario"),
                ("card-device", "<strong>device_code y CIBA</strong> — simulados para completar el discovery"),
            )
        ),
    )
    + "</div></section>"
)

SCENE_BODY["scene-09-uso"] = (
    '<section class="scene">'
    + head("Pasos 5 y 6 del mapa", "UserInfo, API y Revocación")
    + where_you_are("Uso")
    + lanes(
        (
            (
                "OP · OidcMock",
                (
                    ("uso-userinfo", "<code>GET /connect/userinfo</code> devuelve los claims del usuario"),
                    ("uso-introspection", "<code>POST /connect/introspect</code> responde <code>active</code> true o false"),
                    ("uso-revocation", "<code>POST /connect/revocation</code> registra el <code>jti</code>"),
                ),
            ),
            (
                "API",
                (
                    ("uso-api", "Valida la firma contra el JWKS y sirve los datos"),
                ),
            ),
        ),
        0,
    )
    + "</section>"
)

SCENE_BODY["scene-10-sesion"] = (
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

SCENE_BODY["scene-11-avanzados"] = (
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

SCENE_BODY["scene-12-matriz"] = (
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

SCENE_BODY["scene-13-arranque"] = (
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

SCENE_BODY["scene-14-outro"] = (
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
