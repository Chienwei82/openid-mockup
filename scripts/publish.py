#!/usr/bin/env python3
"""Compila OidcMock, ejecuta las tres suites y publica los binarios autocontenidos.

Sustituye a `scripts/test.sh` + `scripts/publish.sh` en un solo comando, porque el flujo de
"dejar esto listo para usar" es siempre el mismo: compilar en Release, comprobar que los tests
pasan y publicar. Es un *wrapper*, no una reimplementacion: el trabajo lo siguen haciendo
`dotnet build` y `dotnet publish`.

Salida con color cuando la consola lo soporta, y cada paso dice que hizo, cuanto tardo y, si
algo falla, las lineas relevantes del log en vez de un volcado de mil lineas.

    ./scripts/publish.py                    # todo: build, tests y publish
    ./scripts/publish.py --skip-tests       # solo compilar y publicar
    ./scripts/publish.py --skip-publish     # build y tests
    ./scripts/publish.py --configuration Debug
    ./scripts/publish.py --runtimes linux-x64

Los artefactos van a `artifacts/publish/<runtime>`, que esta en .gitignore: son binarios
generados y no tienen nada que ver en el historico.
"""

from __future__ import annotations

import argparse
import atexit
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOLUTION = ROOT / "OidcMock.slnx"
HOST_PROJECT = ROOT / "src" / "OidcMock.Host" / "OidcMock.Host.csproj"
ARTIFACTS = ROOT / "artifacts" / "publish"

TEST_PROJECTS = (
    "OidcMock.UnitTests",
    "OidcMock.IntegrationTests",
    "OidcMock.ClientCompatibilityTests",
)

DEFAULT_RUNTIMES = ("linux-x64", "win-x64")


class Console:
    """Color y formato. Sin color si no hay TTY o si el usuario lo pidio con NO_COLOR."""

    def __init__(self) -> None:
        self.color = sys.stdout.isatty() and not os.environ.get("NO_COLOR")

    def _wrap(self, code: str, text: str) -> str:
        return f"\033[{code}m{text}\033[0m" if self.color else text

    def bold(self, text: str) -> str:
        return self._wrap("1", text)

    def green(self, text: str) -> str:
        return self._wrap("32", text)

    def red(self, text: str) -> str:
        return self._wrap("31", text)

    def yellow(self, text: str) -> str:
        return self._wrap("33", text)

    def blue(self, text: str) -> str:
        return self._wrap("36", text)

    def dim(self, text: str) -> str:
        return self._wrap("2", text)

    def step(self, text: str) -> None:
        print(f"\n{self.bold(self.blue('==> ' + text))}", flush=True)

    def ok(self, text: str) -> None:
        print(f"{self.green('  OK')}  {text}", flush=True)

    def warn(self, text: str) -> None:
        print(f"{self.yellow('  --')}  {text}", flush=True)

    def fail(self, text: str) -> None:
        print(f"{self.red('  KO')}  {text}", flush=True)

    def info(self, text: str) -> None:
        print(f"      {self.dim(text)}", flush=True)


def run(command: list[str], log: Path) -> tuple[bool, float]:
    """Ejecuta un comando guardando toda su salida en `log`. Devuelve (ok, segundos)."""
    log.parent.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    with log.open("w", encoding="utf-8", errors="replace") as handle:
        process = subprocess.run(command, cwd=ROOT, stdout=handle, stderr=subprocess.STDOUT)

    return process.returncode == 0, time.monotonic() - started


def size_in_mib(size: int) -> str:
    return f"{size / (1024 * 1024):.0f}"


def shutdown_build_servers() -> None:
    """Cierra los nodos de MSBuild y el compilador Roslyn que deja cada build (nodeReuse).

    Sin esto se quedan vivos indefinidamente y acumulan GB de RAM en un server pequeno.
    `dotnet build-server shutdown` cierra el compilador, pero no siempre los nodos
    /nodeReuse, asi que se rematan con pkill.
    """
    silent = {"stdout": subprocess.DEVNULL, "stderr": subprocess.DEVNULL, "check": False}
    subprocess.run(["dotnet", "build-server", "shutdown"], cwd=ROOT, **silent)
    # El patron lleva corchetes para que pkill no se mate a si mismo ni a la shell que lo invoca.
    subprocess.run(["pkill", "-f", "[n]odemode:1"], **silent)


# Al salir, se terminen bien o mal los pasos: main() tiene returns anticipados cuando algo falla.
atexit.register(shutdown_build_servers)


def relevant_lines(log: Path, limit: int = 15) -> list[str]:
    """Ultimas lineas con pinta de error, que es lo que hace falta para diagnosticar."""
    try:
        text = log.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return []

    # MTP0001 avisa de que `dotnet test --filter` se ignora bajo Testing Platform: es ruido aqui.
    noise = ("warning MTP",)
    hits = [
        line.strip()
        for line in text.splitlines()
        if line.strip()
        and not any(line.startswith(prefix) for prefix in noise)
        and (": error " in line or "[FAIL]" in line or "error run failed" in line)
    ]

    if hits:
        return hits[-limit:]

    return [line for line in text.splitlines() if line.strip()][-limit:]


def report_failure(label: str, log: Path) -> None:
    """Resume el fallo con las lineas del log que explican que paso."""
    print(f"\n{label} fallo. Lineas relevantes de {log.relative_to(ROOT)}:")
    for line in relevant_lines(log):
        print(f"      {line}")


def build(configuration: str, console: Console, logs: Path) -> bool:
    console.step(f"Compilando la solucion en {configuration}")
    command = ["dotnet", "build", str(SOLUTION), "--configuration", configuration, "--nologo"]
    log = logs / "build.log"
    ok, seconds = run(command, log)

    if ok:
        console.ok(f"build correcto en {seconds:.1f}s (0 avisos: TreatWarningsAsErrors)")
    else:
        console.fail(f"build con errores en {seconds:.1f}s")
        report_failure("El build", log)

    return ok


# El runner de Testing Platform colorea su salida con codigos ANSI, asi que la linea de resumen
# llega sucia: hay que buscar los numeros con expresiones regulares y no partiendo por comas.
SUMMARY_PATTERN = re.compile(r"Total:\s*(?P<total>\d+).*?Failed:\s*(?P<failed>\d+)")


def test_summary(log: Path) -> str:
    """Lee la linea de resumen del runner de Testing Platform: 'Total: N, ... Failed: N'."""
    try:
        text = log.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return "sin log"

    matches = list(SUMMARY_PATTERN.finditer(text))
    if not matches:
        return "sin resumen en el log"

    last = matches[-1]
    return f"{last.group('total')} tests, {last.group('failed')} fallos"


def tests(configuration: str, console: Console, logs: Path) -> bool:
    console.step("Ejecutando las tres suites")
    ok = True

    for project in TEST_PROJECTS:
        name = project.removeprefix("OidcMock.")
        log = logs / f"test-{name}.log"
        # --no-build: el runner de xunit.v3 no es VSTest y no encaja con `dotnet test` en varios
        # proyectos a la vez. El build de arriba ya produjo los binarios en la configuracion pedida.
        succeeded, seconds = run(
            ["dotnet", "run", "--project", str(ROOT / "tests" / project),
             "--configuration", configuration, "--no-build"],
            log,
        )

        summary = test_summary(log)
        if succeeded:
            console.ok(f"{name}: {summary} ({seconds:.0f}s)")
        else:
            ok = False
            console.fail(f"{name}: {summary}")
            report_failure(f"Las pruebas de {name}", log)

    return ok


def publish(runtimes: list[str], console: Console, logs: Path) -> bool:
    ok = True

    for runtime in runtimes:
        console.step(f"Publicando {runtime} (autocontenido, un solo archivo)")
        output = ARTIFACTS / runtime
        # Se borra antes: un publish parcial de una version anterior dejaria binarios viejos al lado.
        shutil.rmtree(output, ignore_errors=True)
        log = logs / f"publish-{runtime}.log"

        succeeded, seconds = run(
            ["dotnet", "publish", str(HOST_PROJECT),
             "--configuration", "Release",
             "--runtime", runtime,
             "--self-contained", "true",
             "-p:PublishSingleFile=true",
             "-p:ContinuousIntegrationBuild=true",
             f"-p:PathMap={ROOT}=/_/",
             "--output", str(output)],
            log,
        )

        if succeeded:
            size = sum(f.stat().st_size for f in output.rglob("*") if f.is_file())
            console.ok(f"{runtime} en {output.relative_to(ROOT)} "
                       f"({size_in_mib(size)} MiB, {seconds:.0f}s)")
        else:
            ok = False
            console.fail(f"publish de {runtime} con errores en {seconds:.1f}s")
            report_failure(f"El publish de {runtime}", log)

    return ok


def warn_about_stray_publish(console: Console) -> None:
    """Deja claro por que la salida no va a un 'publish/' en la raiz."""
    if (ROOT / "publish").exists():
        console.warn("Hay un 'publish/' en la raiz: la salida de este script va a artifacts/publish/.")


def main() -> int:
    parser = argparse.ArgumentParser(description="Compila, prueba y publica OidcMock.")
    parser.add_argument("--configuration", "-c", default="Release",
                        help="configuracion de compilacion (Release es la que se publica)")
    parser.add_argument("--runtimes", nargs="+", default=list(DEFAULT_RUNTIMES),
                        help="RID a publicar (por defecto: " + " ".join(DEFAULT_RUNTIMES) + ")")
    parser.add_argument("--skip-tests", action="store_true", help="no ejecutar las suites")
    parser.add_argument("--skip-publish", action="store_true", help="no publicar")
    args = parser.parse_args()

    console = Console()
    logs = ARTIFACTS / "logs"
    started = time.monotonic()

    print(console.bold("OidcMock · compilacion, pruebas y publicacion"))
    console.info(f"raiz: {ROOT}")
    console.info(f"configuracion: {args.configuration}")
    console.info(f"artefactos: {ARTIFACTS.relative_to(ROOT)} (ignorado por git)")
    warn_about_stray_publish(console)

    if not build(args.configuration, console, logs):
        # Publicar sin un build limpio publicaria algo que no se sabe ni que compila.
        console.fail("Se para aqui: no se publica sin un build limpio.")
        return 1

    if args.skip_tests:
        console.warn("Pruebas omitidas por --skip-tests.")
    elif not tests(args.configuration, console, logs):
        # Publicar con pruebas en rojo publicaria algo que no se sabe que funciona.
        console.fail("Se para aqui: no se publica con pruebas en rojo.")
        return 1

    if args.skip_publish:
        console.warn("Publicacion omitida por --skip-publish.")
        return 0

    if not publish(args.runtimes, console, logs):
        console.fail("Se para aqui: al menos un runtime no se publico.")
        return 1

    print(f"\n{console.bold(console.green('Listo.'))} {time.monotonic() - started:.0f}s en total.")
    console.info(f"Ejecutables en {ARTIFACTS.relative_to(ROOT)}/<runtime>/")
    console.info("Apunta la configuracion aparte con OidcMock__ConfigDirectory=... al arrancar.")
    return 0


if __name__ == "__main__":
    sys.exit(main())