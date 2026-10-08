# Explicador OIDC en video (HyperFrames)

Video explicativo de 16:9, en español, que recorre los 12 documentos de
[`docs/openid/`](../) y muestra cómo OidcMock implementa cada pieza del protocolo.

- **Formato:** 1920 × 1080 · 30 fps · H.264
- **Duración:** 177,2 s (12 escenas)
- **Voz:** TTS en español con [`@lobehub/tts`](https://www.npmjs.com/package/@lobehub/tts)
  (voces neuronales de Microsoft Edge) — `es-ES-AlvaroNeural`, `rate: 0.3`
- **Salida:** `dist/oidc-explainer.mp4`

## Requisitos

| Herramienta | Para qué |
| --- | --- |
| Node.js ≥ 22 | HyperFrames, `@lobehub/tts` y `ws` (el WebSocket nativo hace falta para la voz) |
| `ffmpeg` / `ffprobe` | medir y concatenar la narración |
| Python ≥ 3.9 (solo stdlib) | `scripts/build_composition.py` genera el HTML |

```bash
npm install        # hyperframes, @lobehub/tts, ws, yauzl
```

## Estructura

```
index.html              orquestador: root, fondo/marca/progreso, 12 slots + 12 audios
compositions/           una sub-composición por escena (markup + timeline GSAP)
scripts/narration.json  guion por escena: id, kicker, título, texto; voz y rate
scripts/scene_content.py  markup de cada escena (helpers card/list/chips/steps)
scripts/theme.css       tokens y clases compartidas (Material You oscuro)
scripts/generate_audio.mjs    TTS por escena con @lobehub/tts → audio/segments + timings.json
scripts/build_composition.py  timings + markup → index.html y compositions/*.html
audio/segments/         un MP3 por escena (lo que suena en el video)
audio/timings.json      ventana y clip de audio de cada escena (medidos)
audio/narration.mp3     narración completa, solo para revisión
```

Cada escena es una **sub-composición HyperFrames** independiente: el orquestador
solo declara los slots en el tiempo, monta los audios y registra un timeline raíz
mínimo (barra de progreso y barra de marca). Toda la animación de escena vive en
su propio archivo y se registra en `window.__timelines["<scene-id>"]`.

## Regenerar

```bash
npm run audio    # 1. Voz: sintetiza cada escena y mide su duración real
npm run build    # 2. Composición: escribe index.html y compositions/*.html
npm run check    # 3. Validar: lint + runtime + layout + motion + contraste
npm run render   # 4. Renderizar a dist/oidc-explainer.mp4
```

El paso 2 es determinista: si no se toca `narration.json` ni `scene_content.py`,
regenerar produce exactamente los mismos archivos.

> El render tarda unos minutos. Lánzalo con `setsid`/`nohup` si cierras la
> terminal: el proceso de render cancela si detecta que el proceso padre murió.

## Editar una escena

- **Solo el texto narrado:** `scripts/narration.json` → pasos 1 y 2.
- **Solo el contenido en pantalla:** `scripts/scene_content.py` → paso 2
  (los tiempos y la voz no cambian).
- **El estilo de todo el video:** `scripts/theme.css` → paso 2.
- Todo elemento que deba animarse lleva `data-anim`; la escena lo revela en
  cascada (0,45 s + 0,16 s por elemento).

## Motor de voz

La narración se sintetiza con **`@lobehub/tts`** (`EdgeSpeechTTS.createRequest`),
que habla el protocolo de las voces neuronales de Microsoft Edge. Dos detalles
que costaron descubrir y quedan documentados en el código:

1. **Transporte.** `@lobehub/tts` asume un navegador: su `createEdgeSpeech` usa el
   `WebSocket` global y el servicio exige un `Origin` de extensión de Chrome. En
   Node se inyecta `ws` como `globalThis.WebSocket` con las cabeceras del
   handshake; sin el `Origin` correcto el upgrade se queda colgado.
2. **`rate`.** Se traduce a `prosody rate="<rate*100>%"`, pero el servicio lo
   aplica de forma comprimida y lo recorta por encima de `1`. Por eso
   `rate: 0.3` (~19 caracteres/s, ~190 palabras/min) narra a ritmo natural,
   mientras que `rate: 1` se dispara a ~29 caracteres/s.

## Sincronía de audio

`scripts/generate_audio.mjs` sintetiza cada escena por separado y mide su
duración real con `ffprobe`; la ventana de la escena es
`lead-in (0,9 s) + voz + tail (0,7 s)` y el MP3 se ancla dentro de esa ventana
(`data-start` = inicio de la escena + lead-in). Por eso la voz y la imagen no se
desincronizan aunque cambie el texto: los tiempos se recalculan, no se estiman.

## Cobertura del contenido

| Escena | Documento |
| --- | --- |
| 01 Título | introducción a la serie |
| 02 Actores y tokens | [`01-actores-y-tokens`](../01-actores-y-tokens.md) |
| 03 Discovery y JWKS | [`02-discovery-y-jwks`](../02-discovery-y-jwks.md) |
| 04 Authorization Code + PKCE | [`03-authorization-code-y-pkce`](../03-authorization-code-y-pkce.md) |
| 05 ID Token y claims | [`04-id-token-y-claims`](../04-id-token-y-claims.md) |
| 06 Token endpoint y grants | [`05-token-endpoint-y-grants`](../05-token-endpoint-y-grants.md) |
| 07 UserInfo, introspection y revocation | [`06-userinfo`](../06-userinfo.md) · [`07-introspection-y-revocation`](../07-introspection-y-revocation.md) |
| 08 Sesión y logout | [`08-sesion-y-logout`](../08-sesion-y-logout.md) |
| 09 Flujos avanzados | [`09-flujo-hibrido`](../09-flujo-hibrido.md) · [`10-par-device-ciba`](../10-par-device-ciba.md) |
| 10 Matriz de paridad | [`11-matriz-de-paridad`](../11-matriz-de-paridad.md) |
| 11 Arranque y uso | [`12-cheatsheet`](../12-cheatsheet.md) |
| 12 Cierre | resumen y recordatorio de `localhost` |

## Criterios de calidad

`npx hyperframes check .` pasa sin errores ni avisos: lint, runtime, layout
(9 muestras), motion y contraste (89/89 comprobaciones AA).

Ver también [`docs/decisions.md`](../../decisions.md) para el porqué de la
duración y del motor de voz.
