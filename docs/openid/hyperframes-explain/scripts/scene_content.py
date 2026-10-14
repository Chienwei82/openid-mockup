"""Markup interno de cada escena del explicador.

Contrato HyperFrames respetado aquí:
  * sin `<br>` en texto de cuerpo,
  * sin atributos `id` (evita colisiones al ensamblar),
  * todo elemento animado lleva `data-anim` con su variante de entrada
    (`rise`, `pop`, `slide` o `fade`); las piezas de los diagramas llevan
    `data-motion` (`fade|rise|pop|grow|bar|slide|type`) y el build las cronometra
    con la ventana de habla de su `data-hl`,
  * todo elemento que la narración menciona lleva `data-hl="<clave>"`; la
    clave se anuda con las pistas `hl` de scripts/narration.json para que el
    build lo resalte justo cuando se pronuncia su frase.
"""

from __future__ import annotations

import math
from typing import Callable, NamedTuple

class TokenRow(NamedTuple):
    chip: str
    variant: str
    name: str
    minutes: int
    note: str
    hl_intro: str
    hl_life: str


TOKENS: tuple[TokenRow, ...] = (
    TokenRow("code", "code", "authorization_code", 5, "opaco, un solo uso", "tok-code", "row-code"),
    TokenRow("access", "access", "access_token", 30, "JWT RS256, aud = client_id", "tok-access", "row-access"),
    TokenRow("id", "id", "id_token", 30, "solo con scope openid", "tok-id", "row-id"),
    TokenRow("refresh", "refresh", "refresh_token", 480, "rota, requiere offline_access", "tok-refresh", "row-refresh"),
)

TOKEN_VARS: dict[str, str] = {
    "code": "--primary",
    "access": "--secondary",
    "id": "--tertiary",
    "refresh": "--warn",
}

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


def card(title: str, body: str, hl: str | None = None, motion: str | None = None) -> str:
    anim = f'data-motion="{motion}"' if motion else 'data-anim="rise"'
    return (
        f'<article class="card" {anim}{hl_attr(hl)}>'
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


# La tira «estás aquí» es chrome de escena, no contenido: el build la inyecta
# tras la cabecera de cada escena del recorrido con el paso activo de su parada
# del mapa (with_journey_strip en build_composition).
SCENE_NAV: dict[str, str] = {
    "scene-04-discovery": "Discovery",
    "scene-05-pkce": "Authorize",
    "scene-06-idtoken": "Canje",
    "scene-07-canje": "Canje",
    "scene-08-grants": "Canje",
    "scene-09-uso": "Uso",
    "scene-10-sesion": "Uso",
}


# ─────────────────────────────────────────────────────────────────────────────
# Diagramas animados: secuencia (sequence), objetos del protocolo (pkce_pipe,
# journey, jwt_exploded, token_exchange, session_ribbons) y casos de uso
# (usecase).
#
# `sequence` dibuja el mapa como diagrama de secuencia: cuatro carriles de
# actor con identidad propia (color + icono) y mensajes-flecha entre ellos.
# La geometría (centros de carril, ancho de flecha) se precalcula en
# porcentajes aquí, sin medir el DOM en runtime; el build traza cada flecha
# con scaleX 0→1 (receta svg-path-draw adaptada) y asoma punta y cargamento
# de tokens en la ventana de habla del mensaje (receta spring-pop-entrance).
# Los objetos del protocolo llevan `data-motion` y el build los cronometra
# con la ventana de habla de su `data-hl` (receta spring-pop-entrance y
# asr-keyword-glow para el destello de voz).
# `usecase` dibuja una pregunta con ramas de decisión para las escenas de
# casos de uso (grants y flujos avanzados).
# ─────────────────────────────────────────────────────────────────────────────


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


def journey(items: tuple[tuple[str, str, str], ...]) -> str:
    return (
        '<ol class="journey">'
        + "".join(
            f'<li class="jstep" data-motion="rise"{hl_attr(key)}>'
            f'<span class="jstep__n">{index}</span>'
            f'<span class="jstep__body"><strong class="jstep__title">{title}</strong>'
            f'<span class="jstep__note">{note}</span></span></li>'
            for index, (key, title, note) in enumerate(items, start=1)
        )
        + "</ol>"
    )


def pkce_pipe() -> str:
    return (
        '<div class="pkce">'
        '<div class="pkce__row">'
        f'<div class="pkce__piece" data-motion="rise"{hl_attr("step-verifier")}>'
        '<span class="tok tok--verifier">code_verifier</span>'
        '<span class="pkce__note">secreto aleatorio del cliente</span></div>'
        '<div class="pkce__arrow" data-motion="grow"></div>'
        '<div class="pkce__funnel" data-motion="pop">SHA-256</div>'
        '<div class="pkce__arrow" data-motion="grow"></div>'
        f'<div class="pkce__piece" data-motion="rise"{hl_attr("step-verifier")}>'
        '<span class="tok tok--code">code_challenge</span>'
        '<span class="pkce__note">viaja en la URL de authorize</span></div>'
        "</div>"
        f'<p class="pkce__oneway" data-motion="fade"{hl_attr("step-verifier")}>'
        "un solo sentido · el challenge sale del verifier, nunca al revés</p>"
        "</div>"
    )


JWT_SEGMENTS = (
    ("header", "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9", "alg RS256 · typ JWT"),
    ("payload", "eyJzdWIiOiIxMjM0NTY3ODkwIiwibm9uY2UiOiJhODQifQ", "los claims, en claro"),
    ("firma", "SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c", "RS256 · clave pública de JWKS"),
)

JWT_CLAIMS = (
    ("chip-iss", "iss · emisor"),
    ("chip-sub", "sub · usuario"),
    ("chip-aud", "aud · audiencia"),
    ("chip-exp", "exp · caduca"),
    ("chip-iat", "iat · emitido"),
    ("chip-nonce", "nonce · anti-replay"),
)


def jwt_exploded() -> str:
    segments = f'<span class="jwt__dot" data-motion="fade">·</span>'.join(
        f'<div class="jwt__seg jwt__seg--{name}" data-motion="pop">'
        f'<p class="jwt__segname">{name}</p>'
        f'<p class="jwt__b64">{b64}</p>'
        f'<p class="jwt__note">{note}</p></div>'
        for name, b64, note in JWT_SEGMENTS
    )
    return (
        '<div class="jwt">'
        f'<div class="jwt__row">{segments}</div>'
        '<div class="jwt__claims">'
        + "".join(
            f'<span class="chip" data-motion="pop"{hl_attr(key)}>{text}</span>'
            for key, text in JWT_CLAIMS
        )
        + "</div>"
        '<div class="jwt__bind" data-motion="slide" data-hl="chiprow-binding">'
        '<span class="tok tok--access">at_hash</span> ↔ <span class="tok tok--access">access_token</span>'
        '<span class="tok tok--code">c_hash</span> ↔ <span class="tok tok--code">authorization_code</span>'
        "</div>"
        '<article class="card" data-motion="rise" data-hl="card-scope">'
        '<h3 class="card__title">Claims del usuario</h3>'
        '<div class="chiprow">'
        '<span class="chip">name</span><span class="chip">email</span>'
        '<span class="chip">preferred_username</span><span class="chip">locale</span>'
        "</div>"
        '<p class="note">salen del scope concedido</p></article>'
        "</div>"
    )


TOKEN_FAN = (
    ("token--access", "access_token", "30 min", "aud = client_id"),
    ("token--id", "id_token", "30 min", "solo con scope openid"),
    ("token--refresh", "refresh_token", "8 h", "rota en cada uso"),
)


def token_exchange() -> str:
    return (
        '<div class="exchange">'
        '<div class="exchange__out" data-motion="fade" data-hl="paso-envia">'
        '<p class="exchange__label">POST /connect/token</p>'
        '<div class="chiprow">'
        '<span class="chip chip--accent">code</span>'
        '<span class="chip">redirect_uri</span>'
        '<span class="chip">code_verifier</span>'
        "</div></div>"
        '<div class="exchange__checks">'
        '<p class="check" data-motion="pop" data-hl="paso-valida">'
        '<span class="check__mark check__mark--warn">×1</span>'
        "un solo uso: el code se agota aquí</p>"
        '<p class="check" data-motion="pop" data-hl="paso-comprueba">'
        '<span class="check__mark check__mark--ok">✓</span>'
        "comprueba hash(code_verifier) con code_challenge</p>"
        "</div>"
        '<div class="exchange__fan">'
        + "".join(
            f'<article class="token {kind}" data-motion="pop" data-hl="paso-responde">'
            f'<p class="token__name">{name}</p>'
            f'<p class="token__life">{life}</p>'
            f'<p class="token__note">{note}</p></article>'
            for kind, name, life, note in TOKEN_FAN
        )
        + "</div>"
        '<p class="exchange__fail" data-motion="fade" data-hl="paso-repite">'
        '<span class="chip">invalid_grant</span><span class="errow__http">400</span>'
        "repetir el code lo rechaza: ya está inutilizado</p>"
        "</div>"
    )


SESSION_LANES = (
    ("card-op", "Sesión del OP", "habilita el SSO", "op"),
    ("card-cliente", "Sesión del cliente", "su propia cookie", "cliente"),
    ("card-sid", "sid", "puente entre id_token y la sesión del OP", "sid"),
)


def session_ribbons() -> str:
    rows = []
    for key, name, note, kind in SESSION_LANES:
        cut = ""
        if kind != "cliente":
            label = "end_session" if kind == "op" else "sid caduca"
            cut = (
                f'<div class="sess__cut" data-motion="pop"{hl_attr("card-logout")}>'
                f'<span class="badge badge--warn">{label}</span></div>'
            )
        rows.append(
            '<div class="sess__lane" data-motion="fade">'
            f'<div class="sess__who"{hl_attr(key)}>'
            f'<p class="sess__name">{name}</p><p class="sess__note">{note}</p></div>'
            '<div class="sess__track">'
            f'<div class="sess__bar sess__bar--{kind}" data-motion="bar"{hl_attr(key)}></div>'
            f"{cut}</div></div>"
        )
    return (
        '<div class="sess">'
        + "".join(rows)
        + '<p class="note" data-motion="fade" data-hl="card-logout">'
        "logout: <code>connect/end_session</code> con <code>id_token_hint</code> y "
        "<code>post_logout_redirect_uri</code></p>"
        '<div class="chiprow sess__chips" data-motion="fade" data-hl="card-prompt">'
        + "".join(
            f'<span class="chip">{mode}</span>'
            for mode in ("prompt=none", "prompt=login", "prompt=consent", "prompt=select_account")
        )
        + "</div></div>"
    )


# ─────────────────────────────────────────────────────────────────────────────
# Barras de vida de los tokens, cadena de paridad y terminal de arranque.
#
# `life_bars` dibuja cada token como fila: chip + nombre, pista con barra
# proporcional a su vida (escala logarítmica, `life_width`) y metadatos. El
# relleno crece con `data-motion="bar"` en la ventana de habla de su fila.
# `parity_strip` encadena teoría ≡ servidor de referencia ≡ OidcMock.
# `terminal` es una ventana de shell cuyas líneas se «mecanografían» con
# `data-motion="type"` (reveal por clip-path) en la ventana de su comando.
# ─────────────────────────────────────────────────────────────────────────────

LIFE_PEAK_MINUTES = 480  # refresh_token: la vida más larga escala al 100%


def life_width(minutes: int) -> str:
    peak = math.log(LIFE_PEAK_MINUTES + 1)
    return f"{round(100 * math.log(minutes + 1) / peak)}%"


def life_label(minutes: int) -> str:
    return f"{minutes // 60} h" if minutes >= 60 else f"{minutes} min"


def life_rows(rows: tuple[TokenRow, ...], hl_of: Callable[[TokenRow], str]) -> str:
    def one(item: TokenRow) -> str:
        key = hl_of(item)
        return (
            f'<div class="life" data-hl="{key}">'
            f'<p class="life__id"><span class="tok tok--{item.variant}">{item.chip}</span>'
            f'<span class="mono life__name">{item.name}</span></p>'
            f'<div class="life__track"><div class="life__fill" data-motion="bar" data-hl="{key}"'
            f' style="--tok: var({TOKEN_VARS[item.variant]}); width: {life_width(item.minutes)}"></div></div>'
            f'<p class="life__meta"><span class="life__time">{life_label(item.minutes)}</span>'
            f'<span class="life__note">{item.note}</span></p>'
            "</div>"
        )

    return "".join(one(item) for item in rows)


PARITY_NODES = ("Teoría", "Servidor de referencia", "OidcMock")


def parity_strip() -> str:
    nodes = "".join(
        f'<span class="parity__node{" parity__node--mock" if name == "OidcMock" else ""}"'
        f' data-motion="pop">{name}</span>'
        + ('<span class="parity__eq" data-motion="fade">≡</span>' if index < len(PARITY_NODES) - 1 else "")
        for index, name in enumerate(PARITY_NODES)
    )
    return (
        '<div class="parity">'
        + nodes
        + '<span class="badge badge--ok" data-motion="fade">paridad verificada</span>'
        "</div>"
    )


def terminal() -> str:
    def line(text: str, hl: str | None, arg: bool = False) -> str:
        cls = "term__line term__line--arg" if arg else "term__line"
        return (
            f'<p class="{cls}" data-motion="type"{hl_attr(hl)}>'
            f'<span class="term__prompt">{">" if arg else "$"}</span>{text}'
            '<span class="term__caret"></span></p>'
        )

    return (
        '<div class="term" data-motion="rise">'
        '<div class="term__bar">'
        '<span class="term__dot term__dot--r"></span>'
        '<span class="term__dot term__dot--y"></span>'
        '<span class="term__dot term__dot--g"></span>'
        '<span class="term__title">bash — oidcmock</span>'
        "</div>"
        '<div class="term__body">'
        + line("dotnet run --project src/OidcMock.Host", "cmd-run")
        + line("--OidcMock:Issuer=https://localhost:5443/personafisica/", "cmd-issuer", arg=True)
        + line("--OidcMock:Serving:UseHttps=false --OidcMock:Serving:AllowHttp=true", "cmd-issuer", arg=True)
        + "</div></div>"
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
    + card("Cuatro tokens", '<div class="lifebox">' + life_rows(TOKENS, lambda row: row.hl_intro) + "</div>")
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
    + pkce_pipe()
    + journey(
        (
            ("step-authorize", "authorize", "redirige con el code_challenge"),
            ("step-aprueba", "login + consent", "el usuario aprueba en el OP"),
            ("step-canje", "callback", "vuelve al cliente con el code"),
            ("step-compara", "token endpoint", "compara el verifier en tiempo constante"),
        )
    )
    + chips(("plain", "S256 recomendado"), hl="step-metodos")
    + "</section>"
)

SCENE_BODY["scene-06-idtoken"] = (
    '<section class="scene">'
    + head("Paso 4b del mapa", "El ID Token, Despiezado")
    + jwt_exploded()
    + "</section>"
)

SCENE_BODY["scene-07-canje"] = (
    '<section class="scene">'
    + head("Paso 4 del mapa", "El Canje en el Token Endpoint")
    + token_exchange()
    + "</section>"
)

SCENE_BODY["scene-08-grants"] = (
    '<section class="scene">'
    + head("Paso 4, otros caminos", "Otros Grant Types")
    + '<p class="use__q" data-anim="pop">El mismo token endpoint atiende otros caminos</p>'
    + '<div class="grid grid--grants">'
    + card(
        '<span class="tok tok--refresh">refresh_token</span>',
        '<p class="grant__say">pide tokens nuevos sin login</p>'
        '<p class="grant__note">rota en cada uso</p>',
        hl="card-refresh",
        motion="pop",
    )
    + card(
        '<span class="tok tok--access">client_credentials</span>',
        '<p class="grant__say">emite tokens de servicio</p>'
        '<p class="grant__note">sin usuario, con secreto</p>',
        hl="card-client",
        motion="pop",
    )
    + card(
        '<span class="tok tok--code">password</span>',
        '<p class="grant__say">valida usuario y contraseña</p>'
        '<p class="grant__note">directamente, en un solo paso</p>',
        hl="card-password",
        motion="pop",
    )
    + card(
        '<span class="tok tok--id">device_code · CIBA</span>',
        '<p class="grant__say">grants simulados</p>'
        '<p class="grant__note">completan el discovery</p>',
        hl="card-device",
        motion="pop",
    )
    + "</div>"
    + '<p class="note" data-anim="fade">El camino feliz sigue siendo <code>authorization_code</code> + PKCE.</p>'
    + "</section>"
)

SCENE_BODY["scene-09-uso"] = (
    '<section class="scene scene--seq">'
    + head("Pasos 5 y 6 del mapa", "UserInfo, API y Ciclo de Vida")
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
    + session_ribbons()
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
    + parity_strip()
    + '<div class="grid grid--matrix">'
    + '<div class="lifebox">'
    + '<p class="card__label">Vidas de los tokens</p>'
    + life_rows(TOKENS, lambda row: row.hl_life)
    + "</div>"
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
    + '<div class="grid grid--matrix">'
    + '<div class="lifebox">'
    + '<p class="card__label">Comandos</p>'
    + terminal()
    + "</div>"
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
        + chips(("PathBase /personafisica", "ConfigDirectory ./config", "ReloadOnChange true"))
        + '<p class="grant__note">Todo el estado vive en memoria, sin base de datos.</p>',
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
