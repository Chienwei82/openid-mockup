#!/usr/bin/env python3
"""Prueba manual del flujo hibrido de GAUDI: construye la URL de login y desglosa el retorno.

Por defecto apunta al mock local (https://localhost:5443/personafisica/) con el cliente de
prueba; con --host/--client-id se apunta al servidor real. Solo biblioteca estandar: no añade
dependencias al repo (regla 2 de AGENTS.md). Ejemplos:
  ./scripts/oidc-test.py --build
  ./scripts/oidc-test.py "<url Account/Login?ReturnUrl=...>"
  ./scripts/oidc-test.py "<url landing/#code=...&id_token=...>"
  ./scripts/oidc-test.py --decode "<jwt>"
Flags > env (OIDC_HOST, OIDC_PATH_BASE, OIDC_CLIENT_ID, OIDC_RESPONSE_TYPE,
OIDC_SCOPE, OIDC_NONCE, OIDC_SUBJECT, OIDC_CODE_VERIFIER, OIDC_CODE_CHALLENGE,
OIDC_CODE_CHALLENGE_METHOD, OIDC_REDIRECT_URI) > --config JSON > defecto.
"""
from __future__ import annotations

import argparse
import base64
import datetime
import hashlib
import json
import os
import secrets
import sys
from pathlib import Path
from urllib.parse import parse_qsl, quote, unquote, urlsplit

DEFAULTS = {
    "host": "https://localhost:5443",
    "path_base": "/personafisica",
    "client_id": "fb02079c-3143-49e6-a776-dd9b002388d2",
    "response_type": "code id_token",
    "scope": "openid offline_access",
    "nonce": "RandomTextForEcho",
    "subject": "01-2222-3333",
    "code_verifier": "igxfDIwq7GH81kaV2rmZYHU_uNk3wmIZAMyU37Cd75Kgvo6JoUHqudHMl_cWNm38",
    "code_challenge": "mZ9qmLuFWkdJi578Y-Y58DJq3DnTDpbc94EpuHmO658",
    "code_challenge_method": "S256",
    "redirect_uri": "http://localhost:5173/pruebas/ves/landing",
}

ENV_VARS = {
    "host": "OIDC_HOST",
    "path_base": "OIDC_PATH_BASE",
    "client_id": "OIDC_CLIENT_ID",
    "response_type": "OIDC_RESPONSE_TYPE",
    "scope": "OIDC_SCOPE",
    "nonce": "OIDC_NONCE",
    "subject": "OIDC_SUBJECT",
    "code_verifier": "OIDC_CODE_VERIFIER",
    "code_challenge": "OIDC_CODE_CHALLENGE",
    "code_challenge_method": "OIDC_CODE_CHALLENGE_METHOD",
    "redirect_uri": "OIDC_REDIRECT_URI",
}
PARAM_NOTES = {
    "client_id": "para quien es el token (aud del id_token)",
    "response_type": "hibrido GAUDI: code e id_token en el fragmento",
    "scope": "openid pide id_token; offline_access pide refresh_token",
    "nonce": "eco anti-replay, GAUDI lo devuelve en el claim nonce",
    "subject": "subject esperado (claim sub); el del usuario del mock vive en config/users.json",
    "code_verifier": "verificador PKCE: se canjea junto al code",
    "code_challenge": "huella S256 del code_verifier (PKCE RFC7636)",
    "code_challenge_method": "S256 (plain solo en el mock)",
    "redirect_uri": "landing que recibe #code/id_token; debe estar registrada",
}

# Anchos de columna de los desgloses (caracteres).
KEY_COLUMN_WIDTH = 28
CLAIM_COLUMN_WIDTH = 20
PARAM_COLUMN_WIDTH = 22
VALUE_COLUMN_WIDTH = 46

# Truncado central: se muestran los extremos y en medio <head>...<tail> (N car.)
ELLIPSIS = "..."
DEFAULT_HEAD_CHARS = 28
DEFAULT_TAIL_CHARS = 10
ENTRY_HEAD_CHARS = 48
ENTRY_TAIL_CHARS = 12
CALLBACK_HEAD_CHARS = 36
CALLBACK_TAIL_CHARS = 12

TIMESTAMP_CLAIMS = ("iat", "exp", "nbf", "auth_time")
FRAGMENT_PARAMETER_ORDER = ("code", "id_token", "session_state", "state", "error", "error_description")
HIGHLIGHTED_PARAMETERS = ("nonce", "code_challenge")
AUTHORIZE_QUERY_PARAMETERS = ("client_id", "response_type", "scope", "nonce",
                              "code_challenge", "code_challenge_method", "redirect_uri")
CHALLENGE_METHODS = ("S256", "plain")
JWT_SEGMENT_COUNT = 3
UNQUOTE_MAX_ROUNDS = 3
NONCE_RANDOM_BYTES = 16
PKCE_VERIFIER_CHARS = 64


class Console:
    """Color ANSI en la salida. Sin color si no hay TTY o si NO_COLOR esta definido."""

    def __init__(self) -> None:
        self.color = sys.stdout.isatty() and not os.environ.get("NO_COLOR")

    def _wrap(self, code: str, text: str) -> str:
        return f"\033[{code}m{text}\033[0m" if self.color else text

    def bold(self, text: str) -> str:
        return self._wrap("1", text)

    def dim(self, text: str) -> str:
        return self._wrap("2", text)

    def green(self, text: str) -> str:
        return self._wrap("32", text)

    def red(self, text: str) -> str:
        return self._wrap("31", text)

    def yellow(self, text: str) -> str:
        return self._wrap("33", text)

    def cyan(self, text: str) -> str:
        return self._wrap("36", text)

    def magenta(self, text: str) -> str:
        return self._wrap("35", text)
def abbreviate(text: str, full: bool,
               head: int = DEFAULT_HEAD_CHARS, tail: int = DEFAULT_TAIL_CHARS) -> str:
    """Corta el centro dejando los extremos; `full` devuelve el texto entero."""
    if full or len(text) <= head + tail + len(ELLIPSIS):
        return text
    return f"{text[:head]}{ELLIPSIS}{text[-tail:]} ({len(text)} car.)"


def decode_base64url(segment: str) -> bytes:
    """Decodifica un segmento base64url sin padding."""
    padding = "=" * (-len(segment) % 4)
    return base64.urlsafe_b64decode(segment + padding)


def parse_jwt(token: str) -> tuple[dict, dict, str]:
    """Separa un JWT en header, payload y firma SIN validar la firma."""
    parts = token.split(".")
    if len(parts) != JWT_SEGMENT_COUNT:
        raise ValueError(f"JWT con {len(parts)} segmentos, se esperaban {JWT_SEGMENT_COUNT}")
    header = json.loads(decode_base64url(parts[0]).decode())
    payload = json.loads(decode_base64url(parts[1]).decode())
    return header, payload, parts[2]


def format_timestamp(value: object) -> str:
    """Epoch a fecha legible (UTC y hora local); vacio si no es un epoch valido."""
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return ""
    try:
        moment = datetime.datetime.fromtimestamp(value, datetime.timezone.utc)
    except (OverflowError, OSError, ValueError):
        return ""
    return f"{moment:%Y-%m-%d %H:%M:%SZ} ({moment.astimezone():%Y-%m-%d %H:%M %Z})"


def unquote_until_stable(value: str, max_rounds: int = UNQUOTE_MAX_ROUNDS) -> tuple[str, int]:
    """Decodifica %XX hasta que el valor deje de cambiar; devuelve (valor, veces decodificado)."""
    decoded, rounds = value, 0
    for _ in range(max_rounds):
        next_value = unquote(decoded)
        if next_value == decoded:
            break
        decoded, rounds = next_value, rounds + 1
    return decoded, rounds


def generate_pkce_pair() -> tuple[str, str]:
    """Par PKCE (RFC 7636): code_verifier aleatorio y su code_challenge S256 sin padding."""
    verifier = secrets.token_urlsafe(PKCE_VERIFIER_CHARS)[:PKCE_VERIFIER_CHARS]
    digest = hashlib.sha256(verifier.encode()).digest()
    challenge = base64.urlsafe_b64encode(digest).decode().rstrip("=")
    return verifier, challenge


def resolve_config(args: argparse.Namespace) -> dict[str, str]:
    """Valores efectivos por variable: flags > env > --config JSON > DEFAULTS."""
    file_values = {}
    if args.config:
        raw = json.loads(Path(args.config).read_text(encoding="utf-8"))
        if not isinstance(raw, dict):
            raise ValueError("--config debe ser objeto JSON")
        file_values = {key: str(value) for key, value in raw.items() if value is not None}

    def effective(key: str, default: str) -> str:
        flag = getattr(args, key)
        if flag is not None:
            return str(flag)
        return os.environ.get(ENV_VARS[key]) or file_values.get(key, default)

    return {key: effective(key, default) for key, default in DEFAULTS.items()}


def build_login_url(config: dict[str, str]) -> str:
    """URL de /Account/Login con el ReturnUrl codificado que apunta al authorize."""
    query = "&".join(f"{name}={quote(config[name], safe='')}" for name in AUTHORIZE_QUERY_PARAMETERS)
    callback = f"{config['path_base']}/connect/authorize/callback?{query}"
    return f"{config['host'].rstrip('/')}{config['path_base']}/Account/Login?ReturnUrl={quote(callback, safe='')}"


def print_field(console: Console, name: str, value: str, note: str = "") -> None:
    """Fila clave/valor de los desgloses, con una nota opcional al final."""
    label = f"{console.bold(console.cyan(name)):{KEY_COLUMN_WIDTH}}"
    suffix = f"  {console.dim('# ' + note)}" if note else ""
    print(f"  {label} {value}{suffix}")


def encoding_note(rounds: int) -> str:
    """Describe cuantas capas de codificacion de URL tenia el valor."""
    if rounds > 1:
        return f"doble-codificado x{rounds}"
    return "codificado x1" if rounds else ""


def match_note(console: Console, name: str, value: str, config: dict[str, str]) -> str:
    """Contrasta un parametro de la URL con el valor configurado."""
    if name not in config:
        return ""
    if value == config[name]:
        return console.green("= configurado")
    return console.red(f"MODELO ({config[name]})")
def show_login_url(console: Console, url: str, config: dict[str, str]) -> None:
    """Desglosa la URL de login: ReturnUrl crudo, decodificado y sus parametros internos."""
    print(console.bold("\n== URL de entrada (login) =="))
    parts = urlsplit(url)
    print_field(console, "metodo", "GET sin cuerpo (navegacion del navegador)")
    print_field(console, "host", parts.hostname or "")
    print_field(console, "ruta", parts.path)

    raw_return_url = dict(parse_qsl(parts.query, keep_blank_values=True)).get("ReturnUrl", "")
    print_field(console, "ReturnUrl crudo", abbreviate(raw_return_url, True))
    return_url = unquote(raw_return_url)
    print_field(console, "ReturnUrl nivel 1", return_url)

    inner = urlsplit(return_url)
    print_field(console, "callback path", inner.path)
    print("")
    print(console.bold("Parametros internos del ReturnUrl:"))
    print(console.dim(f"  {'parametro':{PARAM_COLUMN_WIDTH}} {'valor':{VALUE_COLUMN_WIDTH}} observacion"))
    for name, raw_value in parse_qsl(inner.query, keep_blank_values=True):
        value, rounds = unquote_until_stable(raw_value)
        note = " ".join(part for part in (
            PARAM_NOTES.get(name, ""),
            encoding_note(rounds),
            match_note(console, name, value, config),
        ) if part)
        column = console.yellow(name) if name in HIGHLIGHTED_PARAMETERS else console.green(name)
        shown = abbreviate(value, False, ENTRY_HEAD_CHARS, ENTRY_TAIL_CHARS)
        print(f"  {column:{PARAM_COLUMN_WIDTH}} {shown:{VALUE_COLUMN_WIDTH}} {console.dim(note)}")
    print("")
    print(console.dim("nonce/code_challenge son los valores fijos de la URL dada, no"))
    print(console.dim("aleatorios de la app. --random-nonce/--new-pkce generan otros."))


def claim_note(console: Console, name: str, value: object, config: dict[str, str]) -> str:
    """Observacion de un claim: fecha legible o contraste con el valor configurado."""
    if name in TIMESTAMP_CLAIMS:
        return format_timestamp(value)
    if name == "nonce" and isinstance(value, str):
        if value == config["nonce"]:
            return console.green("coincide con la entrada")
        return console.red(f"NO coincide (entrada: {config['nonce']})")
    if name == "aud" and isinstance(value, str):
        if value == config["client_id"]:
            return console.green("= client_id")
        return console.red(f"MODELO client_id ({config['client_id']})")
    if name == "sub" and isinstance(value, str):
        if value == config["subject"]:
            return console.green("coincide con el configurado")
        return console.red(f"NO coincide (configurado: {config['subject']})")
    return ""


def show_callback_url(console: Console, url: str, config: dict[str, str],
                      show_jwt: bool, full: bool) -> None:
    """Desglosa la URL de retorno: fragmento, code, id_token y sus claims."""
    print(console.bold("\n== URL de retorno (callback) =="))
    parts = urlsplit(url)
    print_field(console, "base", f"{parts.scheme}://{parts.hostname or ''}{parts.path}")
    print_field(console, "query (?)", parts.query or "(vacia: GAUDI devuelve todo en el fragmento #)")

    fragment = parts.fragment or (url.split("#", 1)[-1] if "#" in url and "code=" in url else "")
    fields = dict(parse_qsl(fragment, keep_blank_values=True))
    print_field(console, "fragmento (#)", f"{len(fields)} parametro(s): {', '.join(fields)}")

    for name in FRAGMENT_PARAMETER_ORDER:
        if name not in fields:
            continue
        value = fields[name] if full else abbreviate(fields[name], False, CALLBACK_HEAD_CHARS, CALLBACK_TAIL_CHARS)
        if name in ("code", "id_token") and not full:
            note = "usa --full para verlo entero"
        elif name == "code":
            note = "canjeable 1 vez en /connect/token"
        else:
            note = ""
        print_field(console, name, value, note)

    if "error" in fields:
        print(console.red(f"\nGAUDI devolvio error: {fields['error']}"))
        if fields.get("error_description"):
            print(f"  {fields['error_description']}")
        return
    if "session_state" in fields:
        print_field(console, "session_state partes",
                    str(len(fields["session_state"].split("."))), "separadas por '.'")
    if "id_token" not in fields:
        print(console.yellow("\nSin id_token en el fragmento: nada que decodificar."))
        return
    if not show_jwt:
        print(console.dim("\nJWT oculto (pasa --jwt para decodificarlo)."))
        return

    print(console.bold("\n-- id_token (firma NO verificada, solo inspeccion) --"))
    try:
        header, payload, signature = parse_jwt(fields["id_token"])
    except ValueError as error:
        print(console.red(f"No se pudo decodificar: {error}"))
        return
    print(f"  {console.magenta('header')}: {json.dumps(header, ensure_ascii=False)}")
    print(f"  {console.magenta('firma')}: {abbreviate(signature, full)}")
    print(f"  {console.magenta('claims')}:")
    for name in sorted(payload):
        note = claim_note(console, name, payload[name], config)
        shown = json.dumps(payload[name], ensure_ascii=False)
        print(f"    {console.bold(console.cyan(name)):{CLAIM_COLUMN_WIDTH}} {shown}  {console.dim(str(note))}")
def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Construye el login GAUDI y desglosa el retorno a color + JWT.",
        epilog="URL con ReturnUrl= es entrada; con #code= es retorno.")
    parser.add_argument("url", nargs="?", help="URL a desglosar. Sin URL: construye la de entrada.")
    parser.add_argument("--build", action="store_true", help="construir la URL de entrada")
    parser.add_argument("--decode", metavar="JWT", help="decodificar un JWT suelto")
    parser.add_argument("--config", metavar="JSON", help="JSON con variables (claves de DEFAULTS)")
    for key in DEFAULTS:
        if key == "code_challenge_method":
            parser.add_argument("--code-challenge-method", dest=key, choices=CHALLENGE_METHODS, default=None)
        else:
            parser.add_argument("--" + key.replace("_", "-"), dest=key, default=None)
    parser.add_argument("--random-nonce", action="store_true")
    parser.add_argument("--new-pkce", action="store_true", help="genera verifier+challenge")
    show_jwt = parser.add_mutually_exclusive_group()
    show_jwt.add_argument("--jwt", dest="jwt", action="store_true")
    show_jwt.add_argument("--no-jwt", dest="jwt", action="store_false")
    parser.set_defaults(jwt=True)
    parser.add_argument("--full", action="store_true", help="code/id_token completos")
    parser.add_argument("--no-color", action="store_true")
    return parser


def run_decode(console: Console, token: str, full: bool) -> int:
    """--decode: desglosa un JWT suelto, sin construir nada mas."""
    try:
        header, payload, signature = parse_jwt(token.strip())
    except ValueError as error:
        print(console.red(f"JWT invalido: {error}"), file=sys.stderr)
        return 2
    print(console.bold("header: ") + json.dumps(header, ensure_ascii=False, indent=2))
    print(console.bold("payload:") + json.dumps(payload, ensure_ascii=False, indent=2))
    print(console.bold("firma:   ") + abbreviate(signature, full))
    for name in TIMESTAMP_CLAIMS:
        if isinstance(payload.get(name), (int, float)):
            print(console.dim(f"{name}: {format_timestamp(payload[name])}"))
    return 0


def show_new_pkce(console: Console, config: dict[str, str]) -> None:
    """Genera un par PKCE, lo fija en la configuracion y lo muestra para guardarlo."""
    verifier, challenge = generate_pkce_pair()
    config["code_verifier"] = verifier
    config["code_challenge"] = challenge
    config["code_challenge_method"] = "S256"
    print(console.bold("PKCE nuevo:"))
    print_field(console, "code_verifier", verifier, "guardalo: se canjea con el code")
    print_field(console, "code_challenge", challenge, "S256 del verifier")


def main() -> int:
    args = build_parser().parse_args()
    console = Console()
    if args.no_color:
        console.color = False

    if args.decode:
        return run_decode(console, args.decode, args.full)

    try:
        config = resolve_config(args)
    except (OSError, ValueError) as error:
        print(console.red(str(error)), file=sys.stderr)
        return 2

    if args.random_nonce:
        config["nonce"] = f"nonce-{secrets.token_urlsafe(NONCE_RANDOM_BYTES)}"
    if args.new_pkce:
        show_new_pkce(console, config)

    if args.url:
        url = args.url.strip().strip("'\"")
        if "ReturnUrl=" in url or "/Account/Login" in url:
            show_login_url(console, url, config)
        else:
            show_callback_url(console, url, config, args.jwt, args.full)
        return 0

    login_url = build_login_url(config)
    print(console.bold("== URL de entrada lista para el navegador =="))
    print(login_url)
    show_login_url(console, login_url, config)
    print("")
    print(console.dim('Al volver: ./scripts/oidc-test.py "<url-retorno>" [--full] [--no-jwt]'))
    return 0


if __name__ == "__main__":
    sys.exit(main())