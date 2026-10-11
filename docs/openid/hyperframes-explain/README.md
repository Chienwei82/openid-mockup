# Explicador OIDC en video (HyperFrames)

Video explicativo de 16:9, en español, que recorre los 12 documentos de
[`docs/openid/`](../) y muestra cómo OidcMock implementa cada pieza del protocolo.

- **Formato:** 1920 × 1080 · 30 fps · H.264
- **Duración:** 264,2 s (12 escenas)
- **Voz:** TTS en español con [`@lobehub/tts`](https://www.npmjs.com/package/@lobehub/tts)
  (voces neuronales de Microsoft Edge) — `es-ES-AlvaroNeural`, `rate: -0.15`
  (≈14 caracteres/s, ritmo de presentador)
- **Salida:** `dist/oidc-explainer.mp4`

## Requisitos

| Herramienta | Para qué |
| --- | --- |
| Node.js ≥ 22 | HyperFrames, `@lobehub/tts` y `ws` (el WebSocket nativo hace falta para la voz) |
| `ffmpeg` / `ffprobe` | medir y concatenar la narración |
| Python ≥ 3.9 (solo stdlib) | `scripts/build_composition.py` genera el HTML |
| `unzip` | extraer el Chrome gestionado que HyperFrames se descarga la primera vez (`npx hyperframes doctor` lo comprueba) |

```bash
npm install        # hyperframes, @lobehub/tts, ws
```

### Avisos de `npm install`

Instalación limpia = instalación sin avisos. Los dos que salían y su arreglo:

| Aviso | Causa | Arreglo |
| --- | --- | --- |
| `ERESOLVE overriding peer dependency` (react 19 frente a `@emoji-mart/react`) | `@lobehub/tts` declara peers de navegador (`react`, `react-dom`, `antd`, `@lobehub/ui`) que aquí no se usan: la narración solo importa su entrada de Node | `.npmrc` con `legacy-peer-deps=true`; el árbol pasa de ~560 a ~230 paquetes |
| `allow-scripts … esbuild@0.25.12 (postinstall: node install.js)` | npm ≥ 11 exige aprobar los scripts de instalación de las dependencias | campo `allowScripts` en `package.json`, sin fijar versión para que sobreviva a los updates |

`npx hyperframes doctor` debe salir en verde salvo las herramientas opcionales
(transcripción, TTS y música locales). Lo único que el CLI no trae consigo es
`unzip` (en Windows el extractor viene integrado): lo usa para descomprimir el
Chrome gestionado que descarga la primera vez, y el doctor lo comprueba.

## Estructura

```
index.html              orquestador: root, fondo/marca/progreso, 12 slots + 12 audios
compositions/           una sub-composición por escena (markup + timeline GSAP)
scripts/narration.json  guion por escena: id, kicker, título, texto, pistas hl; voz y rate
scripts/scene_content.py  markup de cada escena (helpers card/list/chips/steps + data-hl)
scripts/theme.css       tokens y clases compartidas (Material You oscuro)
scripts/generate_audio.mjs    TTS + marcas de palabra → audio/segments + timings.json
scripts/build_composition.py  timings + markup → index.html y compositions/*.html
scripts/probe_voice.mjs       sonda de prosodia y puntuación para ajustar la voz
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

> El render tarda unos minutos (~7 min en software GPU para 264 s de video) y se
> cancela solo (`render_cancelled_parent_exited`) si muere cualquier proceso
> ancestro. Para lanzarlo desde una terminal que se va a cerrar, repáralo a
> `init`: `HYPERFRAMES_RENDER_DETACHED=1 setsid --fork nohup npx hyperframes render . -o dist/oidc-explainer.mp4`.

## Servidor de desarrollo (Studio)

```bash
npm run dev      # alias de `npm run preview` → Studio en http://localhost:3002
npm run stop     # detiene el servidor en segundo plano
```

`npm run dev` levanta el Studio de HyperFrames con recarga automática: sirve
`index.html` y sus 12 sub-composiciones, lint en cada guardado y timeline
navegable para revisar antes de renderizar. En una terminal interactiva se queda
en primer plano (Ctrl+C para parar); si la shell no es interactiva, el CLI lo deja
en segundo plano y se cierra con `npm run stop`.

> **Primera pasada:** el Studio —y también `npm run check`— **estampa** los ids
> `data-hf-id` en `index.html` y `compositions/*.html`. El estampado es
> determinista e idempotente: la primera vez aparece como cambio en git y las
> siguientes no. `npm run build` vuelve a escribir esos HTML sin ids, así que hay
> que elegir: versionar los ids (y entonces `check`/`dev` dejan el árbol limpio) o
> restaurar con `npm run build` antes de commitear. El estado local del Studio
> (`.hyperframes/`) está en `.gitignore`.

## Editar una escena

- **Solo el texto narrado:** `scripts/narration.json` → pasos 1 y 2.
- **Solo el contenido en pantalla:** `scripts/scene_content.py` → paso 2
  (los tiempos y la voz no cambian).
- **El estilo de todo el video:** `scripts/theme.css` → paso 2.
- Todo elemento que deba animarse lleva `data-anim` con su variante de entrada
  (`rise`, `pop`, `slide` o `fade`); la escena lo revela en cascada (0,45 s y un
  escalonado pautado según la duración de la voz).
- **Resaltado sincronizado con la voz:** un elemento con `data-hl="<clave>"` se
  resalta mientras la narración pronuncia la frase `say` anudada a esa clave en el
  campo `hl` de la escena (`narration.json`).

## Motor de voz

La narración se sintetiza con **`@lobehub/tts`** (`EdgeSpeechTTS.createRequest`),
que habla el protocolo de las voces neuronales de Microsoft Edge. Tres detalles
que costaron descubrir, medidos con `scripts/probe_voice.mjs` y documentados en
el código:

1. **Transporte.** `@lobehub/tts` asume un navegador: su `createEdgeSpeech` usa el
   `WebSocket` global y el servicio exige un `Origin` de extensión de Chrome. En
   Node se inyecta `ws` como `globalThis.WebSocket` con las cabeceras del
   handshake; sin el `Origin` correcto el upgrade se queda colgado.
2. **`rate` es un cambio RELATIVO.** Se traduce a `prosody rate="<rate*100>%"`,
   pero el servicio lo aplica como ± porcentaje sobre el ritmo por defecto de la
   voz (≈16,5 caracteres/s), no como fracción del mismo. Así, `rate: 0.3` narra
   +30% más rápido (≈21 chars/s, ~210 palabras/min — demasiado para un
   explicador), sin prosody narra a 16,5 chars/s y `rate: -0.15` a ≈14 chars/s
   (~140 palabras/min): el ritmo natural de presentador que usa el guion.
3. **Lo que el servicio ignora o rechaza.** `pitch` no tiene efecto audible, y el
   SSML con `<break>` o `<mstts:express-as style>` se rechaza (el turno termina
   sin audio). Las pausas se consiguen solo con la puntuación del guion: una coma
   añade ≈ +0,5 s de respiro y un punto y aparte ≈ +1 s.

## Sincronía de audio

`scripts/generate_audio.mjs` sintetiza cada escena por separado y mide su
duración real con `ffprobe`; la ventana de la escena es
`lead-in (0,9 s) + voz + tail (1 s)` y el MP3 se ancla dentro de esa ventana
(`data-start` = inicio de la escena + lead-in). Por eso la voz y la imagen no se
desincronizan aunque cambie el texto: los tiempos se recalculan, no se estiman.

Además, el servicio emite marcas `WordBoundary` por cada palabra narrada. Se
capturan en el wrapper de WebSocket y `generate_audio.mjs` las usa para resolver,
para cada pista `hl` del guion, la ventana exacta `[inicio, fin]` en la que se
pronuncia su frase `say`. Esas ventanas viajan a `audio/timings.json` y
`build_composition.py` las convierte en los resaltados de pantalla sincronizados
con la voz. Si faltaran las marcas, la ventana se estima por posición de
caracteres y se avisa por consola.

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
