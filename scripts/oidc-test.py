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
import argparse, base64, datetime, hashlib, json, os, secrets, sys
from pathlib import Path
from urllib.parse import parse_qsl, quote, unquote, urlsplit

DEFAULTS = {"host": "https://localhost:5443", "path_base": "/personafisica",
 "client_id": "fb02079c-3143-49e6-a776-dd9b002388d2", "response_type": "code id_token",
 "scope": "openid offline_access", "nonce": "RandomTextForEcho", "subject": "01-2222-3333",
 "code_verifier": "igxfDIwq7GH81kaV2rmZYHU_uNk3wmIZAMyU37Cd75Kgvo6JoUHqudHMl_cWNm38",
 "code_challenge": "mZ9qmLuFWkdJi578Y-Y58DJq3DnTDpbc94EpuHmO658",
 "code_challenge_method": "S256",
 "redirect_uri": "http://localhost:5173/pruebas/ves/landing"}
ENVS = {"host": "OIDC_HOST", "path_base": "OIDC_PATH_BASE", "client_id": "OIDC_CLIENT_ID",
 "response_type": "OIDC_RESPONSE_TYPE", "scope": "OIDC_SCOPE", "nonce": "OIDC_NONCE",
 "subject": "OIDC_SUBJECT",
 "code_verifier": "OIDC_CODE_VERIFIER", "code_challenge": "OIDC_CODE_CHALLENGE",
 "code_challenge_method": "OIDC_CODE_CHALLENGE_METHOD",
 "redirect_uri": "OIDC_REDIRECT_URI"}
HELP = {"client_id": "para quien es el token (aud del id_token)",
 "response_type": "hibrido GAUDI: code e id_token en el fragmento",
 "scope": "openid pide id_token; offline_access pide refresh_token",
 "nonce": "eco anti-replay, GAUDI lo devuelve en el claim nonce",
 "subject": "subject esperado (claim sub); el del usuario del mock vive en config/users.json",
 "code_verifier": "verificador PKCE: se canjea junto al code",
 "code_challenge": "huella S256 del code_verifier (PKCE RFC7636)",
 "code_challenge_method": "S256 (plain solo en el mock)",
 "redirect_uri": "landing que recibe #code/id_token; debe estar registrada"}

class C:
    def __init__(self): self.on = sys.stdout.isatty() and not os.environ.get("NO_COLOR")
    def w(self, code, t): return f"\033[{code}m{t}\033[0m" if self.on else t
    def b(self, t): return self.w("1", t)
    def d(self, t): return self.w("2", t)
    def g(self, t): return self.w("32", t)
    def r(self, t): return self.w("31", t)
    def y(self, t): return self.w("33", t)
    def c(self, t): return self.w("36", t)
    def m(self, t): return self.w("35", t)

def resolve(a):
    f = {}
    if a.config:
        d = json.loads(Path(a.config).read_text(encoding="utf-8"))
        if not isinstance(d, dict): raise SystemExit("--config debe ser objeto JSON")
        f = {k: str(v) for k, v in d.items() if v is not None}
    return {k: (str(getattr(a, k)) if getattr(a, k) is not None else
        os.environ.get(ENVS[k]) or f.get(k, dflt)) for k, dflt in DEFAULTS.items()}

def build_entry(cfg):
    inner = "&".join(f"{k}={quote(cfg[k], safe='')}" for k in
        ("client_id", "response_type", "scope", "nonce", "code_challenge",
         "code_challenge_method", "redirect_uri"))
    cb = f"{cfg['path_base']}/connect/authorize/callback?{inner}"
    return f"{cfg['host'].rstrip('/')}{cfg['path_base']}/Account/Login?ReturnUrl={quote(cb, safe='')}"

def stable(v, n=3):
    c, k = v, 0
    for _ in range(n):
        u = unquote(c)
        if u == c: break
        c, k = u, k + 1
    return c, k

def b64(s): return base64.urlsafe_b64decode(s + "=" * (-len(s) % 4))

def jwt(t):
    p = t.split(".")
    if len(p) != 3: raise ValueError(f"JWT con {len(p)} segmentos, se esperaban 3")
    return json.loads(b64(p[0]).decode()), json.loads(b64(p[1]).decode()), p[2]

def stamp(ts):
    if isinstance(ts, bool) or not isinstance(ts, (int, float)): return ""
    try: m = datetime.datetime.fromtimestamp(ts, datetime.timezone.utc)
    except (OverflowError, OSError, ValueError): return ""
    return f"{m:%Y-%m-%d %H:%M:%SZ} ({m.astimezone():%Y-%m-%d %H:%M %Z})"

def cut(t, full, h=28, tl=10):
    return t if full or len(t) <= h + tl + 3 else f"{t[:h]}...{t[-tl:]} ({len(t)} car.)"

def kv(c, k, v, note=""):
    print(f"  {c.b(c.c(k)):28} {v}" + (f"  {c.d('# ' + note)}" if note else ""))
#PART2
def show_entry(c, url, cfg):
    print(c.b("\n== URL de entrada (login) =="))
    s = urlsplit(url)
    kv(c, "metodo", "GET sin cuerpo (navegacion del navegador)")
    kv(c, "host", s.hostname or "")
    kv(c, "ruta", s.path)
    raw = dict(parse_qsl(s.query, keep_blank_values=True)).get("ReturnUrl", "")
    kv(c, "ReturnUrl crudo", cut(raw, True))
    lv1 = unquote(raw)
    kv(c, "ReturnUrl nivel 1", lv1)
    inner = urlsplit(lv1)
    kv(c, "callback path", inner.path)
    print("")
    print(c.b("Parametros internos del ReturnUrl:"))
    print(c.d(f"  {'parametro':22} {'valor':46} observacion"))
    for name, r in parse_qsl(inner.query, keep_blank_values=True):
        v, n = stable(r)
        mark = ""
        if name in cfg:
            mark = c.g("= configurado") if v == cfg[name] else c.r(f"MODELO ({cfg[name]})")
        extra = f"doble-codificado x{n}" if n > 1 else ("codificado x1" if n else "")
        note = " ".join(x for x in (HELP.get(name, ""), extra, mark) if x)
        col = c.y(name) if name in ("nonce", "code_challenge") else c.g(name)
        print(f"  {col:22} {cut(v, False, 48, 12):46} {c.d(note)}")
    print("")
    print(c.d("nonce/code_challenge son los valores fijos de la URL dada, no"))
    print(c.d("aleatorios de la app. --random-nonce/--new-pkce generan otros."))

def show_cb(c, url, cfg, want_jwt, full):
    print(c.b("\n== URL de retorno (callback) =="))
    s = urlsplit(url)
    kv(c, "base", f"{s.scheme}://{s.hostname or ''}{s.path}")
    kv(c, "query (?)", s.query or "(vacia: GAUDI devuelve todo en el fragmento #)")
    frag = s.fragment or (url.split("#", 1)[-1] if "#" in url and "code=" in url else "")
    fp = dict(parse_qsl(frag, keep_blank_values=True))
    kv(c, "fragmento (#)", f"{len(fp)} parametro(s): {', '.join(fp)}")
    for n in ("code", "id_token", "session_state", "state", "error", "error_description"):
        if n in fp: kv(c, n, fp[n] if full else cut(fp[n], False, 36, 12),
            "usa --full para verlo entero" if n in ("code", "id_token") and not full else
            ("canjeable 1 vez en /connect/token" if n == "code" else ""))
    if "error" in fp:
        print(c.r(f"\nGAUDI devolvio error: {fp['error']}"))
        if fp.get("error_description"): print(f"  {fp['error_description']}")
        return
    if "session_state" in fp:
        kv(c, "session_state partes", str(len(fp["session_state"].split("."))), "separadas por '.'")
    if "id_token" not in fp:
        print(c.y("\nSin id_token en el fragmento: nada que decodificar."))
        return
    if not want_jwt:
        print(c.d("\nJWT oculto (pasa --jwt para decodificarlo)."))
        return
    print(c.b("\n-- id_token (firma NO verificada, solo inspeccion) --"))
    try: h, p, sig = jwt(fp["id_token"])
    except (ValueError, json.JSONDecodeError) as e:
        print(c.r(f"No se pudo decodificar: {e}"))
        return
    print(f"  {c.m('header')}: {json.dumps(h, ensure_ascii=False)}")
    print(f"  {c.m('firma')}: {cut(sig, full)}")
    print(f"  {c.m('claims')}:")
    for k in sorted(p):
        v, note = p[k], ""
        if k in ("iat", "exp", "nbf", "auth_time"): note = stamp(v)
        if k == "nonce" and isinstance(v, str):
            note = c.g("coincide con la entrada") if v == cfg["nonce"] else c.r(f"NO coincide (entrada: {cfg['nonce']})")
        if k == "aud" and isinstance(v, str):
            note = c.g("= client_id") if v == cfg["client_id"] else c.r(f"MODELO client_id ({cfg['client_id']})")
        if k == "sub" and isinstance(v, str):
            note = c.g("coincide con el configurado") if v == cfg["subject"] else c.r(f"NO coincide (configurado: {cfg['subject']})")
        print(f"    {c.b(c.c(k)):20} {json.dumps(v, ensure_ascii=False)}  {c.d(str(note))}")
def pkce():
    v = secrets.token_urlsafe(64)[:64]
    ch = base64.urlsafe_b64encode(hashlib.sha256(v.encode()).digest()).decode().rstrip("=")
    return v, ch

def parser():
    p = argparse.ArgumentParser(description="Construye el login GAUDI y desglosa el retorno a color + JWT.",
        epilog="URL con ReturnUrl= es entrada; con #code= es retorno.")
    p.add_argument("url", nargs="?", help="URL a desglosar. Sin URL: construye la de entrada.")
    p.add_argument("--build", action="store_true", help="construir la URL de entrada")
    p.add_argument("--decode", metavar="JWT", help="decodificar un JWT suelto")
    p.add_argument("--config", metavar="JSON", help="JSON con variables (claves de DEFAULTS)")
    for k in DEFAULTS:
        if k == "code_challenge_method":
            p.add_argument("--code-challenge-method", dest=k, choices=["S256", "plain"], default=None)
        else:
            p.add_argument("--" + k.replace("_", "-"), dest=k, default=None)
    p.add_argument("--random-nonce", action="store_true")
    p.add_argument("--new-pkce", action="store_true", help="genera verifier+challenge")
    g = p.add_mutually_exclusive_group(); g.add_argument("--jwt", dest="jwt", action="store_true")
    g.add_argument("--no-jwt", dest="jwt", action="store_false"); p.set_defaults(jwt=True)
    p.add_argument("--full", action="store_true", help="code/id_token completos")
    p.add_argument("--no-color", action="store_true")
    return p

def main():
    a = parser().parse_args()
    c = C()
    if a.no_color: c.on = False
    if a.decode:
        try: h, p, s = jwt(a.decode.strip())
        except (ValueError, json.JSONDecodeError) as e:
            print(c.r(f"JWT invalido: {e}"), file=sys.stderr)
            return 2
        print(c.b("header: ") + json.dumps(h, ensure_ascii=False, indent=2))
        print(c.b("payload:") + json.dumps(p, ensure_ascii=False, indent=2))
        print(c.b("firma:   ") + cut(s, a.full))
        for k in ("iat", "exp", "nbf", "auth_time"):
            if isinstance(p.get(k), (int, float)): print(c.d(f"{k}: {stamp(p[k])}"))
        return 0
    try: cfg = resolve(a)
    except SystemExit as e:
        print(c.r(str(e)), file=sys.stderr)
        return 2
    if a.random_nonce: cfg["nonce"] = f"nonce-{secrets.token_urlsafe(16)}"
    if a.new_pkce:
        v, ch = pkce()
        cfg["code_verifier"] = v; cfg["code_challenge"] = ch; cfg["code_challenge_method"] = "S256"
        print(c.b("PKCE nuevo:")); kv(c, "code_verifier", v, "guardalo: se canjea con el code")
        kv(c, "code_challenge", ch, "S256 del verifier")
    if a.url:
        u = a.url.strip().strip("'\"")
        if "ReturnUrl=" in u or "/Account/Login" in u: show_entry(c, u, cfg)
        else: show_cb(c, u, cfg, a.jwt, a.full)
        return 0
    e = build_entry(cfg)
    print(c.b("== URL de entrada lista para el navegador ==")); print(e)
    show_entry(c, e, cfg)
    print(""); print(c.d('Al volver: ./scripts/oidc-test.py "<url-retorno>" [--full] [--no-jwt]'))
    return 0

if __name__ == "__main__": sys.exit(main())
