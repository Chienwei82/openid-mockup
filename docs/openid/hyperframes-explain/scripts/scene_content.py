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
# Diagramas animados: flujo secuencial (flow) y casos de uso (usecase).
#
# `flow` dibuja una cadena de paradas unidas por un riel que el build traza
# (receta svg-path-draw) y un token viajero por parada (receta
# spring-pop-entrance + driver lineal seek-safe). Cada parada lleva `data-hl`
# para que la voz la encienda al mencionarla (receta asr-keyword-glow).
# `usecase` dibuja una pregunta con ramas de decisión para las escenas de
# casos de uso (grants, sesiones, flujos avanzados, contenido del id token).
# ─────────────────────────────────────────────────────────────────────────────

def flow_stop(number: str, label: str, hl: str | None = None) -> str:
    return (
        f'<li class="flow__stop" data-anim="pop"{hl_attr(hl)}>'
        f'<span class="flow__n">{number}</span>'
        f'<span class="flow__label">{label}</span>'
        f'<span class="flow__token" data-token="{hl or number}" aria-hidden="true"></span>'
        "</li>"
    )


def flow(stops: tuple[tuple[str, str, str | None], ...]) -> str:
    return (
        '<ol class="flow" data-anim="fade">'
        '<span class="flow__rail" aria-hidden="true"></span>'
        + "".join(flow_stop(number, label, key) for number, label, key in stops)
        + "</ol>"
    )


def usecase(
    question: str,
    branches: tuple[tuple[str, str, str, str | None], ...],
) -> str:
    return (
        '<div class="use">'
        f'<p class="use__q" data-anim="pop">{question}</p>'
        '<div class="use__branches">'
        + "".join(
            f'<article class="use__branch" data-anim="rise"{hl_attr(hl)}>'
            f'<h3 class="use__when">{when}</h3>'
            f'<p class="use__go">{go}</p>'
            f'<p class="use__note">{note}</p>'
            "</article>"
            for when, go, note, hl in branches
        )
        + "</div></div>"
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
    _ = active
    return chips(tuple(MAP_ACTORS)) + flow(
        tuple((str(number), label, key) for number, (key, label) in enumerate(FLOW_STOPS, start=1))
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
    + flow(
        (
            ("1", "El cliente pide <code>/.well-known/openid-configuration</code>", "card-discovery"),
            ("2", "Lee issuer, endpoints y capacidades soportadas", "card-discovery"),
            ("3", "Descarga el <strong>JWKS</strong> con las claves públicas RS256", "card-jwks"),
            ("4", "Ya sabe a dónde redirigir y con qué validar firmas", "card-jwks"),
        )
    )
    + "</section>"
)

SCENE_BODY["scene-05-pkce"] = (
    '<section class="scene">'
    + head("Pasos 2 y 3 del mapa", "Authorization Code + PKCE")
    + where_you_are("Authorize")
    + flow(
        (
            ("1", "El cliente genera <code>verifier</code> y <code>challenge</code>", "step-verifier"),
            ("2", "Redirige a <code>/connect/authorize</code> con challenge y <code>state</code>", "step-authorize"),
            ("3", "El usuario <strong>aprueba</strong> y vuelve con el <code>code</code>", "step-aprueba"),
            ("4", "Al canjear, el servidor <strong>compara</strong> el verifier en tiempo constante", "step-compara"),
            ("5", "Métodos <code>plain</code> y <code>S256</code>: usa <strong>S256</strong>", "step-metodos"),
        )
    )
    + "</section>"
)

SCENE_BODY["scene-06-idtoken"] = (
    '<section class="scene">'
    + head("Paso 4b del mapa", "El ID Token")
    + where_you_are("Canje")
    + usecase(
        "¿Qué trae el id token?",
        (
            ("Identidad", "<code>sub</code>, <code>name</code>, <code>email</code>", "quién es el usuario", "card-claims"),
            ("Validez", "<code>iss</code>, <code>aud</code>, <code>exp</code>, <code>iat</code>", "quién lo emite y hasta cuándo vale", "card-tiempos"),
            ("Sesión", "<code>sid</code>, <code>auth_time</code>, <code>nonce</code>", "a qué login pertenece", "card-sesion"),
        ),
    )
    + '<p class="note" data-anim="fade">Solo aparece cuando pides el scope <code>openid</code>.</p>'
    + "</section>"
)

SCENE_BODY["scene-07-canje"] = (
    '<section class="scene">'
    + head("Paso 4 del mapa", "El Canje en el Token Endpoint")
    + where_you_are("Canje")
    + flow(
        (
            ("1", "El cliente envía <strong>code</strong> más <code>verifier</code>", "canje-post"),
            ("2", "El servidor <strong>valida</strong> el challenge", "canje-valida"),
            ("3", "Emite <strong>access</strong>, <strong>id</strong> y <strong>refresh</strong>", "canje-tokens"),
            ("4", "El code queda <strong>inutilizado</strong>: un solo uso", "canje-un-solo-uso"),
        )
    )
    + '<p class="note" data-anim="fade">El code expira en 5 minutos y es de un solo uso.</p>'
    + "</section>"
)

SCENE_BODY["scene-08-grants"] = (
    '<section class="scene">'
    + head("Paso 4, otros caminos", "Otros Grant Types")
    + where_you_are("Canje")
    + usecase(
        "¿Qué grant uso en cada caso?",
        (
            ("Hay usuario y navegador", "<strong>authorization_code</strong> con PKCE", "el camino feliz ya visto", "card-code"),
            ("Solo máquina a máquina", "<strong>client_credentials</strong>", "sin usuario, con secreto", "card-cc"),
            ("La sesión sigue viva", "<strong>refresh_token</strong>", "rota el refresh en cada uso", "card-refresh"),
        ),
    )
    + '<p class="note" data-anim="fade">OidcMock también anuncia password, device_code y CIBA.</p>'
    + "</section>"
)

SCENE_BODY["scene-09-uso"] = (
    '<section class="scene">'
    + head("Pasos 5 y 6 del mapa", "UserInfo, API y Ciclo de Vida")
    + where_you_are("Uso")
    + flow(
        (
            ("5", "<strong>UserInfo</strong> devuelve los claims del usuario", "uso-userinfo"),
            ("6", "La <strong>API</strong> valida firma, issuer, aud y expiración", "uso-api"),
            ("7", "<strong>Introspection</strong> responde siempre active sí o no", "uso-introspection"),
            ("8", "<strong>Revocation</strong> registra el jti o revoca la familia", "uso-revocation"),
        )
    )
    + "</section>"
)

SCENE_BODY["scene-10-sesion"] = (
    '<section class="scene">'
    + head("Sesión", "Tres Sesiones y el Logout")
    + usecase(
        "¿Qué sesión se cierra con el logout?",
        (
            ("Sesión del OP", "habilita el <strong>SSO</strong> entre clientes", "cookie del servidor", "card-op"),
            ("Sesión del cliente", "su propia <strong>cookie</strong> de app", "vive aunque el OP cierre", "card-cliente"),
            ("El sid puente", "conecta el <strong>id token</strong> con el OP", "viaja en el logout", "card-sid"),
        ),
    )
    + '<p class="note" data-anim="fade">El logout usa <code>connect/endsession</code> con id token hint; '
    + 'el <code>prompt</code> admite <code>none</code>, <code>login</code>, <code>consent</code> o <code>select_account</code>.</p>'
    + "</section>"
)

SCENE_BODY["scene-11-avanzados"] = (
    '<section class="scene">'
    + head("Avanzado", "Flujos Avanzados")
    + usecase(
        "¿Qué flujo avanzado necesito?",
        (
            ("Parámetros sensibles", "<strong>PAR</strong>: request uri por back-channel", "implementado", "card-par"),
            ("Sistema interno", "<strong>híbrido</strong> code id token", "valida c_hash y session_state", "card-hibrido"),
            ("Sin pantalla ni push", "<strong>device</strong> y <strong>CIBA</strong> simulados", "sondeo con slow_down", "card-device"),
        ),
    )
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
