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

from typing import NamedTuple

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
# Diagramas animados: secuencia (sequence), flujo (flow) y casos de uso
# (usecase).
#
# `sequence` dibuja el mapa como diagrama de secuencia: cuatro carriles de
# actor con identidad propia (color + icono) y mensajes-flecha entre ellos.
# La geometría (centros de carril, ancho de flecha) se precalcula en
# porcentajes aquí, sin medir el DOM en runtime; el build traza cada flecha
# con scaleX 0→1 (receta svg-path-draw adaptada) y asoma punta y cargamento
# de tokens en la ventana de habla del mensaje (receta spring-pop-entrance).
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
# Identidad de actores: color e icono fijos, reutilizados en tarjetas de rol,
# carriles del diagrama de secuencia y futuros diagramas de cada escena.
ACTOR_VARS: dict[str, str] = {
    "owner": "--actor-owner",
    "cliente": "--actor-client",
    "navegador": "--actor-nav",
    "op": "--actor-op",
    "api": "--actor-api",
}

ACTOR_ICONS: dict[str, str] = {
    "owner": '<circle cx="12" cy="8" r="3.4"/><path d="M5.5 19.5c.6-3.4 3.3-5 6.5-5s5.9 1.6 6.5 5"/>',
    "cliente": '<rect x="4.5" y="5.5" width="15" height="13" rx="2.2"/><path d="M4.5 9.5h15"/><circle cx="7.6" cy="7.5" r=".9"/>',
    "navegador": '<circle cx="12" cy="12" r="7.5"/><path d="M12 4.5c-4 4.6-4 10.4 0 15"/><path d="M4.9 9.5h14.2M4.9 14.5h14.2"/>',
    "op": '<circle cx="9" cy="12" r="3.2"/><path d="M12.2 12H20"/><path d="M17 12v3.2"/><path d="M14.4 12v2.2"/>',
    "api": '<rect x="4.5" y="5" width="15" height="5.6" rx="1.6"/><rect x="4.5" y="13.4" width="15" height="5.6" rx="1.6"/><path d="M8 7.8h.01M8 16.2h.01"/>',
}

SEQ_LANES: tuple[tuple[str, str, str], ...] = (
    ("cliente", "Cliente", "la aplicación"),
    ("navegador", "Navegador", "el usuario"),
    ("op", "OP · OidcMock", "emisor de tokens"),
    ("api", "API", "la API protegida"),
)

LANE_INDEX: dict[str, int] = {actor: index for index, (actor, _name, _note) in enumerate(SEQ_LANES)}


def actor_icon(actor: str) -> str:
    return (
        '<svg class="actor__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" '
        'stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">'
        f"{ACTOR_ICONS[actor]}</svg>"
    )


def role_card(actor: str, hl: str, name: str, note: str) -> str:
    return (
        f'<article class="role" data-anim="pop" style="--actor: var({ACTOR_VARS[actor]})"{hl_attr(hl)}>'
        f'<span class="role__icon">{actor_icon(actor)}</span>'
        f'<h3 class="role__name">{name}</h3>'
        f'<p class="role__note">{note}</p>'
        "</article>"
    )


class SeqMsg(NamedTuple):
    sender: str
    receiver: str
    label: str
    hl: str
    tokens: tuple[str, ...] = ()
    caption: str = ""


def lane_center(actor: str) -> float:
    return 12.5 + 25 * LANE_INDEX[actor]


def seq_actor(actor: str, name: str, note: str, off: bool) -> str:
    return (
        f'<div class="seq__actor{" seq__actor--off" if off else ""}" data-anim="pop"'
        f' style="--actor: var({ACTOR_VARS[actor]})">'
        f'<span class="role__icon">{actor_icon(actor)}</span>'
        f'<p class="seq__name">{name}</p>'
        f'<p class="seq__sub">{note}</p>'
        "</div>"
    )


def seq_life(actor: str, off: bool) -> str:
    return (
        f'<span class="seq__life{" seq__life--off" if off else ""}"'
        f' style="color: var({ACTOR_VARS[actor]})"></span>'
    )


def seq_cargo(msg: SeqMsg, tip_x: float) -> str:
    if not msg.tokens:
        return ""
    cargo = "".join(f'<span class="tok tok--{kind} seq__token">{kind}</span>' for kind in msg.tokens)
    return f'<span class="seq__chips" style="left:{tip_x}%">{cargo}</span>'


def seq_message(msg: SeqMsg) -> str:
    sender_x = lane_center(msg.sender)
    receiver_x = lane_center(msg.receiver)
    direction = "lr" if sender_x < receiver_x else "rl"
    anchor = f"left:{sender_x}%" if direction == "lr" else f"right:{100 - sender_x}%"
    caption = f'<span class="seq__cap">{msg.caption}</span>' if msg.caption else ""
    tall = " seq__msg--tall" if msg.caption else ""
    return (
        f'<div class="seq__msg seq__msg--{direction}{tall}" style="color: var({ACTOR_VARS[msg.sender]})">'
        f'<span class="seq__label" style="{anchor}"{hl_attr(msg.hl)}>'
        f'<span class="seq__text">{msg.label}</span>{caption}</span>'
        f'<span class="seq__line" style="left:{min(sender_x, receiver_x)}%;'
        f'width:{abs(receiver_x - sender_x)}%" aria-hidden="true"></span>'
        f'<span class="seq__tip" style="left:{receiver_x}%" aria-hidden="true"></span>'
        + seq_cargo(msg, receiver_x)
        + "</div>"
    )


def sequence(messages: tuple[SeqMsg, ...], dimmed: tuple[str, ...] = ()) -> str:
    actors = "".join(
        seq_actor(actor, name, note, actor in dimmed) for actor, name, note in SEQ_LANES
    )
    lives = "".join(seq_life(actor, actor in dimmed) for actor, _name, _note in SEQ_LANES)
    return (
        '<div class="seq" data-anim="fade">'
        f'<div class="seq__actors">{actors}</div>'
        '<div class="seq__stage">'
        f'<div class="seq__lives" aria-hidden="true">{lives}</div>'
        + "".join(seq_message(message) for message in messages)
        + "</div></div>"
    )


# El mapa fijo: el flujo completo en ocho flechas, con sus seis claves de voz.
MAP_MESSAGES: tuple[SeqMsg, ...] = (
    SeqMsg("cliente", "op", "<code>GET</code> /.well-known/openid-configuration", "flujo-discovery"),
    SeqMsg("cliente", "navegador", "<code>302</code> → /connect/authorize", "flujo-authorize"),
    SeqMsg("navegador", "op", "login + consent", "flujo-aprueba"),
    SeqMsg("op", "cliente", "<code>302</code> /callback", "flujo-aprueba", ("code",)),
    SeqMsg("cliente", "op", "<code>POST</code> /connect/token", "flujo-canje", ("code", "verifier")),
    SeqMsg("op", "cliente", "el servidor emite los tokens", "flujo-canje", ("access", "id", "refresh")),
    SeqMsg("cliente", "op", "<code>GET</code> /connect/userinfo", "flujo-userinfo"),
    SeqMsg("cliente", "api", "<code>GET</code> /api con Bearer", "flujo-api", ("access",)),
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
    + '<div class="roles">'
    + role_card("owner", "role-owner", "Resource Owner", "el usuario, dueño de sus datos")
    + role_card("cliente", "role-client", "Client", "la aplicación que pide acceso")
    + role_card("op", "role-as", "Authorization Server", "OidcMock emite los tokens")
    + role_card("api", "role-rs", "Resource Server", "la API protegida por tokens")
    + "</div>"
    + '<div class="grid">'
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
    '<section class="scene scene--seq">'
    + head("El mapa", "El Flujo Completo, en Seis Pasos")
    + sequence(MAP_MESSAGES)
    + '<p class="note" data-anim="fade">Memoriza este mapa: cada escena siguiente amplía una de sus paradas.</p>'
    + "</section>"
)

SCENE_BODY["scene-04-discovery"] = (
    '<section class="scene scene--seq">'
    + head("Paso 1 del mapa", "Discovery y JWKS")
    + where_you_are("Discovery")
    + sequence(
        (
            SeqMsg(
                "cliente",
                "op",
                "<code>GET</code> /.well-known/openid-configuration",
                "card-discovery",
                (),
                "issuer, endpoints y capacidades soportadas",
            ),
            SeqMsg(
                "cliente",
                "op",
                "<code>GET</code> /.well-known/openid-configuration/jwks",
                "card-jwks",
                (),
                "claves públicas RS256 para validar firmas",
            ),
        ),
        dimmed=("navegador", "api"),
    )
    + '<p class="note" data-anim="fade">Ya sabe a dónde redirigir y con qué validar firmas.</p>'
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
    '<section class="scene scene--seq">'
    + head("Pasos 5 y 6 del mapa", "UserInfo, API y Ciclo de Vida")
    + where_you_are("Uso")
    + sequence(
        (
            SeqMsg(
                "cliente",
                "op",
                "<code>GET</code> /connect/userinfo",
                "uso-userinfo",
                (),
                "devuelve los claims del usuario",
            ),
            SeqMsg(
                "cliente",
                "api",
                "<code>GET</code> /api con Bearer",
                "uso-api",
                ("access",),
                "valida firma, issuer, aud y expiración",
            ),
            SeqMsg(
                "cliente",
                "op",
                "<code>POST</code> /connect/introspect",
                "uso-introspection",
                (),
                "responde siempre active sí o no",
            ),
            SeqMsg(
                "cliente",
                "op",
                "<code>POST</code> /connect/revocation",
                "uso-revocation",
                (),
                "registra el jti o revoca la familia",
            ),
        ),
        dimmed=("navegador",),
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
