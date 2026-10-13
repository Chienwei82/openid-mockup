"""Genera el orquestador index.html y las sub-composiciones de cada escena.

Uso:  python scripts/build_composition.py

Lee `audio/timings.json` (producido por generate_audio.py) y escribe:
  index.html
  compositions/<scene-id>.html
"""

from __future__ import annotations

import json
import re
from pathlib import Path

from scene_content import SCENE_BODY

ROOT = Path(__file__).resolve().parent.parent
COMPOSITIONS_DIR = ROOT / "compositions"
WIDTH = 1920
HEIGHT = 1080
FPS = 30
FIRST_ITEM_DELAY = 0.45
STAGGER_MIN = 0.15
STAGGER_MAX = 0.24
STAGGER_SPREAD = 0.30
MSG_SHARED_DELAY = 0.9
VISUAL_TRACK = 1
AUDIO_TRACK = 10

# Cada escena anima sus entradas con cuatro variantes (data-anim="rise|pop|
# slide|fade") y resalta en pantalla lo que la voz está pronunciando: un único
# driver lineal recorre las ventanas de habla de timings.json y pinta cada
# elemento [data-hl] con una envolvente ataque-sostén-caída (adaptado de la
# receta asr-keyword-glow de hyperframes-animation). Solo se pintan bordes y
# sombras de texto: nada que pueda empeorar el contraste AA auditado.
# Los diagramas de flujo añaden dos piezas seek-safe al mismo timeline:
# el riel (.flow__rail) se traza con scaleY 0→1 (receta svg-path-draw
# adaptada: sin getTotalLength, sin dashoffset) y cada token viajero
# (.flow__token) aparece con pop + glow justo en la ventana de habla de su
# parada (receta spring-pop-entrance sobre el driver lineal). Los diagramas
# de secuencia trazan sus flechas (.seq__line) con scaleX 0→1 y asoman punta
# y cargamento (.seq__tip, .seq__token) en la ventana de habla del mensaje;
# las filas que comparten clave de voz se revelan escalonadas
# (MSG_SHARED_DELAY). Solo transforms (scale) + opacity: nada de
# top/left/width en los tweens.
SCENE_TEMPLATE = """<template>
  <div
    class="scene-root"
    data-composition-id="__SCENE_ID__"
    data-width="__WIDTH__"
    data-height="__HEIGHT__"
    data-duration="__DURATION__"
  >
__BODY__

    <script>
      (function () {
        var root = document.querySelector('[data-composition-id="__SCENE_ID__"]');
        if (!root || typeof gsap === 'undefined') { return; }
        var timeline = gsap.timeline({ paused: true });

        var FROM = {
          rise: { opacity: 0, y: 36 },
          pop: { opacity: 0, y: 22, scale: 0.92 },
          slide: { opacity: 0, x: -40 },
          fade: { opacity: 0 },
        };
        var items = root.querySelectorAll('[data-anim]');
        for (var index = 0; index < items.length; index += 1) {
          var from = FROM[items[index].getAttribute('data-anim')] || FROM.rise;
          timeline.from(
            items[index],
            Object.assign({ duration: 0.65, ease: 'power3.out' }, from),
            __FIRST_DELAY__ + index * __STAGGER__
          );
        }

        var HIGHLIGHTS = __HIGHLIGHTS__;
        var RAILS = __RAILS__;
        var TOKENS = __TOKENS__;
        var rails = root.querySelectorAll('.flow__rail');
        for (var rail = 0; rail < rails.length; rail += 1) {
          var span = RAILS[rail] || [0, 0.6];
          timeline.fromTo(
            rails[rail],
            { scaleY: 0 },
            { scaleY: 1, duration: Math.max(span[1] - span[0], 0.4), ease: 'power2.out' },
            span[0]
          );
        }
        var tokens = root.querySelectorAll('.flow__token');
        for (var tok = 0; tok < tokens.length; tok += 1) {
          var win = TOKENS[tok] || [0, 0];
          timeline.fromTo(
            tokens[tok],
            { opacity: 0, scale: 0 },
            { opacity: 1, scale: 1, duration: 0.35, ease: 'power3.out' },
            Math.max(win[0] - 0.15, 0)
          );
          timeline.to(tokens[tok], { opacity: 0.25, duration: 0.5, ease: 'power1.out' }, win[1]);
        }
        var MSGS = __MSGS__;
        var msgs = root.querySelectorAll('.seq__msg');
        for (var msg = 0; msg < msgs.length; msg += 1) {
          var span = MSGS[msg] || [0, 0.6];
          timeline.fromTo(
            msgs[msg].querySelector('.seq__line'),
            { scaleX: 0 },
            { scaleX: 1, duration: 0.55, ease: 'power2.inOut' },
            span[0]
          );
          timeline.fromTo(
            msgs[msg].querySelector('.seq__label'),
            { opacity: 0 },
            { opacity: 1, duration: 0.3, ease: 'power1.out' },
            span[0] + 0.08
          );
          var tip = msgs[msg].querySelector('.seq__tip');
          if (tip) {
            timeline.fromTo(tip, { opacity: 0 }, { opacity: 1, duration: 0.18, ease: 'none' }, span[0] + 0.48);
          }
          var cargo = msgs[msg].querySelectorAll('.seq__token');
          for (var cargoIndex = 0; cargoIndex < cargo.length; cargoIndex += 1) {
            timeline.fromTo(
              cargo[cargoIndex],
              { opacity: 0, scale: 0 },
              { opacity: 1, scale: 1, duration: 0.35, ease: 'back.out(1.7)' },
              span[0] + 0.62 + cargoIndex * 0.14
            );
          }
        }
        var targets = [];
        Object.keys(HIGHLIGHTS).forEach(function (key) {
          var els = root.querySelectorAll('[data-hl="' + key + '"]');
          for (var hl = 0; hl < els.length; hl += 1) {
            targets.push({ el: els[hl], windows: HIGHLIGHTS[key] });
          }
        });
        var ATTACK = 0.22;
        var RELEASE = 0.55;
        function envelope(time, start, end) {
          if (time < start) { return 0; }
          if (time < end) { return Math.min((time - start) / ATTACK, 1); }
          if (time < end + RELEASE) { return 1 - (time - end) / RELEASE; }
          return 0;
        }
        function paint(target, env) {
          var el = target.el;
          if (env <= 0.001) {
            el.style.borderColor = '';
            el.style.boxShadow = '';
            el.style.textShadow = '';
            return;
          }
          el.style.borderColor =
            'rgb(' + Math.round(58 + 19 * env) + ', ' + Math.round(63 + 145 * env) + ', ' + Math.round(71 + 154 * env) + ')';
          el.style.boxShadow = '0 0 ' + (36 * env).toFixed(1) + 'px rgba(77, 208, 225, ' + (0.28 * env).toFixed(3) + ')';
          el.style.textShadow = '0 0 ' + (16 * env).toFixed(1) + 'px rgba(77, 208, 225, ' + (0.4 * env).toFixed(3) + ')';
        }
        var driver = { t: 0 };
        timeline.to(
          driver,
          {
            t: __DURATION__,
            duration: __DURATION__,
            ease: 'none',
            onUpdate: function () {
              for (var i = 0; i < targets.length; i += 1) {
                var env = 0;
                for (var w = 0; w < targets[i].windows.length; w += 1) {
                  env = Math.max(env, envelope(driver.t, targets[i].windows[w][0], targets[i].windows[w][1]));
                }
                paint(targets[i], env);
              }
            },
          },
          0
        );

        window.__timelines['__SCENE_ID__'] = timeline;
      })();
    </script>
  </div>
</template>
"""

INDEX_TEMPLATE = """<!doctype html>
<html lang="es">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=__WIDTH__, height=__HEIGHT__" />
    <title>OpenID Connect Explicado — OidcMock</title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link
      rel="stylesheet"
      href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&amp;family=JetBrains+Mono:wght@400;500&amp;display=swap"
    />
    <script src="https://cdn.jsdelivr.net/npm/gsap@3.14.2/dist/gsap.min.js"></script>
    <style>
__THEME__

      [data-composition-src] { position: absolute; inset: 0; width: 100%; height: 100%; }
    </style>
  </head>
  <body>
    <div
      id="root"
      data-composition-id="main"
      data-start="0"
      data-width="__WIDTH__"
      data-height="__HEIGHT__"
      data-fps="__FPS__"
      data-duration="__TOTAL__"
    >
      <!-- Fondo, marca y riel de progreso: viven toda la composición -->
      <div class="hf-backdrop" data-layout-allow-overflow>
        <div class="hf-backdrop__glow hf-backdrop__glow--primary"></div>
        <div class="hf-backdrop__glow hf-backdrop__glow--tertiary"></div>
        <div class="hf-backdrop__grid"></div>
      </div>
      <header class="hf-brandbar">
        <span class="hf-brandbar__mark"><span class="hf-brandbar__dot"></span>OidcMock · OpenID Connect</span>
        <span class="hf-brandbar__tag">/personafisica</span>
      </header>
      <div class="hf-rail__label">Explicador OIDC · __SCENECOUNT__ escenas</div>
      <div class="hf-rail"><div class="hf-rail__fill"></div></div>

      <!-- Escenas: cada una es una sub-composición independiente -->
__SLOTS__

      <!-- Narración: un clip de audio por escena, alineado con su ventana -->
__AUDIOS__

      <script>
        (function () {
          var root = document.getElementById('root');
          var total = parseFloat(root.getAttribute('data-duration'));
          var timeline = gsap.timeline({ paused: true });
          timeline.fromTo('.hf-rail__fill', { scaleX: 0 }, { scaleX: 1, duration: total, ease: 'none' }, 0);
          timeline.from('.hf-brandbar', { opacity: 0, y: -18, duration: 0.7, ease: 'power2.out' }, 0.15);
          timeline.from('.hf-rail__label', { opacity: 0, duration: 0.6, ease: 'power2.out' }, 0.35);
          window.__timelines['main'] = timeline;
        })();
      </script>
    </div>
  </body>
</html>
"""

def reveal_stagger(body: str, spoken: float) -> float:
    items = max(body.count("data-anim=") - 1, 1)
    spread = (spoken * STAGGER_SPREAD) / items
    return round(min(STAGGER_MAX, max(STAGGER_MIN, spread)), 2)


def highlight_windows(scene: dict[str, object], lead_in: float) -> dict[str, list[list[float]]]:
    grouped: dict[str, list[list[float]]] = {}
    for highlight in scene["highlights"]:
        grouped.setdefault(str(highlight["key"]), []).append(
            [
                round(lead_in + float(highlight["start"]), 3),
                round(lead_in + float(highlight["end"]), 3),
            ]
        )
    return grouped


def flow_motion(scene: dict[str, object], lead_in: float) -> tuple[list[list[float]], list[list[float]]]:
    windows = highlight_windows(scene, lead_in)
    body = SCENE_BODY[str(scene["id"])]
    tokens = re.findall(r'data-token="([^"]+)"', body)
    rails = len(re.findall(r'class="flow__rail"', body))
    bounds = [window for key in tokens for window in windows.get(key, [])]
    span = [bounds[0][0], bounds[-1][1]] if bounds else [round(lead_in, 3), round(lead_in + 0.6, 3)]
    return (
        [span for _ in range(rails)],
        [windows.get(key, [span])[0] for key in tokens],
    )


def seq_motion(scene: dict[str, object], lead_in: float) -> list[list[float]]:
    windows = highlight_windows(scene, lead_in)
    body = SCENE_BODY[str(scene["id"])]
    keys = re.findall(r'class="seq__label"[^>]*data-hl="([^"]+)"', body)
    fallback = [round(lead_in, 3), round(lead_in + 0.6, 3)]
    spans: list[list[float]] = []
    used: dict[str, int] = {}
    for key in keys:
        window = windows.get(key, [fallback])[0]
        offset = used.get(key, 0) * MSG_SHARED_DELAY
        used[key] = used.get(key, 0) + 1
        spans.append([round(window[0] + offset, 3), round(window[1] + offset, 3)])
    return spans


def build_scene(scene: dict[str, object], lead_in: float) -> None:
    scene_id = str(scene["id"])
    body = SCENE_BODY[scene_id]
    windows = highlight_windows(scene, lead_in)
    missing = [key for key in windows if f'data-hl="{key}"' not in body]
    if missing:
        print(f"aviso: {scene_id} resalta claves sin elemento data-hl: {missing}")
    rails, tokens = flow_motion(scene, lead_in)
    messages = seq_motion(scene, lead_in)
    document = (
        SCENE_TEMPLATE.replace("__SCENE_ID__", scene_id)
        .replace("__WIDTH__", str(WIDTH))
        .replace("__HEIGHT__", str(HEIGHT))
        .replace("__DURATION__", str(scene["duration"]))
        .replace("__BODY__", body)
        .replace("__FIRST_DELAY__", str(FIRST_ITEM_DELAY))
        .replace("__STAGGER__", str(reveal_stagger(body, float(scene["audioDuration"]))))
        .replace("__HIGHLIGHTS__", json.dumps(windows))
        .replace("__RAILS__", json.dumps(rails))
        .replace("__TOKENS__", json.dumps(tokens))
        .replace("__MSGS__", json.dumps(messages))
    )
    (COMPOSITIONS_DIR / f"{scene_id}.html").write_text(document, encoding="utf-8")


def build_orchestrator(scenes: list[dict[str, object]], total: float) -> None:
    slots = "\n".join(
        "      <div\n"
        f'        id="el-{scene["id"]}"\n'
        f'        data-composition-id="{scene["id"]}"\n'
        f'        data-composition-src="compositions/{scene["id"]}.html"\n'
        f'        data-start="{scene["start"]}"\n'
        f'        data-duration="{scene["duration"]}"\n'
        f'        data-track-index="{VISUAL_TRACK}"\n'
        "      ></div>"
        for scene in scenes
    )
    audios = "\n".join(
        "      <audio\n"
        f'        id="audio-{scene["id"]}"\n'
        f'        src="{scene["audio"]}"\n'
        f'        data-start="{scene["audioStart"]}"\n'
        f'        data-duration="{scene["audioDuration"]}"\n'
        f'        data-track-index="{AUDIO_TRACK}"\n'
        "      ></audio>"
        for scene in scenes
    )

    theme = (ROOT / "scripts" / "theme.css").read_text(encoding="utf-8").rstrip()
    document = (
        INDEX_TEMPLATE
        .replace("__THEME__", theme)
        .replace("__WIDTH__", str(WIDTH))
        .replace("__HEIGHT__", str(HEIGHT))
        .replace("__FPS__", str(FPS))
        .replace("__TOTAL__", str(total))
        .replace("__SCENECOUNT__", str(len(scenes)))
        .replace("__SLOTS__", slots)
        .replace("__AUDIOS__", audios)
    )
    (ROOT / "index.html").write_text(document, encoding="utf-8")


def main() -> None:
    timings = json.loads((ROOT / "audio" / "timings.json").read_text(encoding="utf-8"))
    scenes = timings["scenes"]

    missing = [scene["id"] for scene in scenes if scene["id"] not in SCENE_BODY]
    if missing:
        raise SystemExit(f"Escenas sin markup en scene_content.py: {missing}")

    COMPOSITIONS_DIR.mkdir(exist_ok=True)
    for scene in scenes:
        build_scene(scene, float(timings["leadIn"]))
    build_orchestrator(scenes, timings["total"])
    print(f"index.html + {len(scenes)} composiciones · duración {timings['total']}s")


if __name__ == "__main__":
    main()


