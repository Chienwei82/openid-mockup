"""Genera el orquestador index.html y las sub-composiciones de cada escena.

Uso:  python scripts/build_composition.py

Lee `audio/timings.json` (producido por generate_audio.py) y escribe:
  index.html
  compositions/<scene-id>.html
"""

from __future__ import annotations

import json
from pathlib import Path

from scene_content import SCENE_BODY

ROOT = Path(__file__).resolve().parent.parent
COMPOSITIONS_DIR = ROOT / "compositions"
WIDTH = 1920
HEIGHT = 1080
FPS = 30
FIRST_ITEM_DELAY = 0.45
STAGGER = 0.16
VISUAL_TRACK = 1
AUDIO_TRACK = 10

SCENE_TEMPLATE = """<template>
  <div
    class="scene-root"
    data-composition-id="{scene_id}"
    data-width="{width}"
    data-height="{height}"
    data-duration="{duration}"
  >
{body}

    <script>
      (function () {{
        var root = document.querySelector('[data-composition-id="{scene_id}"]');
        if (!root || typeof gsap === 'undefined') {{ return; }}
        var items = root.querySelectorAll('[data-anim]');
        var timeline = gsap.timeline({{ paused: true }});
        for (var index = 0; index < items.length; index += 1) {{
          timeline.from(
            items[index],
            {{ opacity: 0, y: 36, duration: 0.65, ease: 'power3.out' }},
            {first_delay} + index * {stagger}
          );
        }}
        window.__timelines['{scene_id}'] = timeline;
      }})();
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
      <div class="hf-rail__label">Explicador OIDC · 12 escenas</div>
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

def build_scene(scene: dict[str, object]) -> None:
    scene_id = str(scene["id"])
    document = SCENE_TEMPLATE.format(
        scene_id=scene_id,
        width=WIDTH,
        height=HEIGHT,
        duration=scene["duration"],
        body=SCENE_BODY[scene_id],
        first_delay=FIRST_ITEM_DELAY,
        stagger=STAGGER,
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
        build_scene(scene)
    build_orchestrator(scenes, timings["total"])
    print(f"index.html + {len(scenes)} composiciones · duración {timings['total']}s")


if __name__ == "__main__":
    main()


