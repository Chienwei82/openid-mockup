/**
 * Narración TTS por escena con @lobehub/tts (voces neuronales de Microsoft Edge).
 *
 * Salida:
 *   audio/segments/seg-NN.mp3   un clip de audio por escena
 *   audio/narration.mp3         narración completa (solo para revisión)
 *   audio/timings.json          ventanas medidas de cada escena + marcas de palabra
 *
 * Cada escena reserva un lead-in visual (la tarjeta aparece antes de que hable
 * la voz) y un tail de respiro antes del corte siguiente.
 *
 * Sobre `rate` (medido con scripts/probe_voice.mjs): el servicio trata el
 * porcentaje de <prosody rate> como CAMBIO RELATIVO sobre el ritmo por defecto
 * de la voz (≈16,5 caracteres/s). Así, `rate: 0.3` → `rate="30%"` narra +30%
 * más rápido (≈21 chars/s, demasiado para un explicador) y `rate: -0.15` →
 * `rate="-15%"` narra ≈14 chars/s (≈140 palabras/min), ritmo de presentador.
 * `pitch` lo ignora el servicio, y `<break>`/`mstts:express-as` rechaza el
 * SSML: las pausas se consiguen solo con la puntuación del guion.
 *
 * Además de la voz, este script captura las marcas `WordBoundary` que el
 * servicio emite (`metadataoptions.wordBoundaryEnabled`) y resuelve, para cada
 * entrada `hl` del guion, la ventana [inicio, fin] en la que se narra su frase
 * (`say`). Esa ventana se guarda en timings.json y el build la usa para
 * resaltar el elemento `data-hl` correspondiente justo cuando se menciona.
 *
 * Uso:  node scripts/generate_audio.mjs
 */

import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { EdgeSpeechTTS } from '@lobehub/tts';
import WebSocket from 'ws';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const AUDIO_DIR = join(ROOT, 'audio');
const SEGMENTS_DIR = join(AUDIO_DIR, 'segments');
const LEAD_IN_SECONDS = 0.9;
const TAIL_SECONDS = 1.0;
const SYNTHESES_TIMEOUT_MS = 60_000;
const TICKS_PER_SECOND = 10_000_000n;

// @lobehub/tts habla el protocolo de Edge TTS, pero su transporte asume un
// navegador: en Node hay que aportar el WebSocket (ws) y el Origin que el
// servicio exige, o el handshake se queda colgado.
const EDGE_HANDSHAKE_HEADERS = {
  Origin: 'chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold',
  'User-Agent':
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0',
  Pragma: 'no-cache',
  'Cache-Control': 'no-cache',
  'Accept-Language': 'en-US,en;q=0.9',
};

// Las marcas de palabra llegan como mensajes `Path:audio.metadata` por el mismo
// WebSocket; @lobehub/tts las descarta, así que se capturan aquí en el wrapper.
const wordBoundaries = [];

globalThis.WebSocket = class NodeEdgeWebSocket extends WebSocket {
  constructor(url, protocols) {
    super(url, protocols, { headers: EDGE_HANDSHAKE_HEADERS });
    this.addEventListener('message', (event) => {
      if (typeof event.data !== 'string') return;
      const separator = event.data.indexOf('\r\n\r\n');
      if (separator < 0) return;
      if (!event.data.slice(0, separator).includes('Path:audio.metadata')) return;
      for (const entry of JSON.parse(event.data.slice(separator + 4)).Metadata ?? []) {
        if (entry.Type !== 'WordBoundary') continue;
        wordBoundaries.push({
          text: entry.Data.text.Text,
          start: Number(entry.Data.Offset) / Number(TICKS_PER_SECOND),
          duration: Number(entry.Data.Duration) / Number(TICKS_PER_SECOND),
        });
      }
    });
  }
};

const round = (value) => Math.round(value * 1000) / 1000;

function probeDuration(file) {
  const stdout = execFileSync(
    'ffprobe',
    ['-v', 'error', '-show_entries', 'format=duration', '-of', 'default=noprint_wrappers=1:nokey=1', file],
    { encoding: 'utf8' },
  );
  return Number.parseFloat(stdout.trim());
}

async function synthesize(text, voice, rate, target) {
  wordBoundaries.length = 0;
  const response = await Promise.race([
    EdgeSpeechTTS.createRequest({
      payload: { input: text, options: { voice, rate } },
    }),
    new Promise((resolve) => setTimeout(() => resolve(null), SYNTHESES_TIMEOUT_MS)),
  ]);
  if (!response) {
    throw new Error(`El servicio TTS no devolvió audio para "${target}"`);
  }
  writeFileSync(target, Buffer.from(await response.arrayBuffer()));
  return wordBoundaries.slice();
}

// ── Ventanas de resaltado ────────────────────────────────────────────────────
// Cada entrada `hl` del guion declara { key, say }: `key` es un elemento
// `data-hl` del markup de la escena y `say` la frase del texto narrado cuyo
// momento de habla activa el resaltado. La ventana se resuelve contra las
// marcas de palabra reales; si faltan, se estima por posición de caracteres.

const stripAccents = (text) => text.normalize('NFD').replace(/[\u0300-\u036f]/g, '');

const tokensOf = (text) =>
  stripAccents(text)
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
    .split(' ')
    .filter(Boolean);

function spokenTokenStream(words) {
  const stream = [];
  for (const word of words) {
    for (const token of tokensOf(word.text)) {
      stream.push({ token, start: word.start, end: round(word.start + word.duration) });
    }
  }
  return stream;
}

function findWindow(stream, needles, fromIndex) {
  for (let index = fromIndex; index <= stream.length - needles.length; index += 1) {
    const matches = needles.every((needle, offset) => stream[index + offset].token === needle);
    if (matches) {
      return {
        start: stream[index].start,
        end: stream[index + needles.length - 1].end,
        nextIndex: index + needles.length,
      };
    }
  }
  return null;
}

function estimateWindow(text, say, spoken) {
  const haystack = stripAccents(text).toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();
  const needle = stripAccents(say).toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();
  const index = haystack.indexOf(needle);
  if (index < 0) return null;
  return {
    start: round((index / haystack.length) * spoken),
    end: round(((index + needle.length) / haystack.length) * spoken),
  };
}

function resolveHighlights(scene, words, spoken) {
  const stream = spokenTokenStream(words);
  const highlights = [];
  let cursor = 0;
  for (const cue of scene.hl ?? []) {
    const window = findWindow(stream, tokensOf(cue.say), cursor);
    const resolved = window ?? estimateWindow(scene.text, cue.say, spoken);
    if (!resolved) {
      console.warn(`  aviso: sin ventana de habla para "${cue.say}" (${cue.key}) en ${scene.id}`);
      continue;
    }
    if (window) cursor = window.nextIndex;
    highlights.push({ key: cue.key, start: resolved.start, end: resolved.end });
  }
  return highlights;
}

function concatenate(segments, target) {
  const listFile = join(AUDIO_DIR, 'concat.txt');
  writeFileSync(listFile, segments.map((file) => `file '${file}'\n`).join(''), 'utf8');
  execFileSync('ffmpeg', [
    '-y',
    '-v',
    'error',
    '-f',
    'concat',
    '-safe',
    '0',
    '-i',
    listFile,
    '-ar',
    '44100',
    '-ac',
    '2',
    '-b:a',
    '192k',
    target,
  ]);
  rmSync(listFile, { force: true });
}

async function main() {
  const script = JSON.parse(readFileSync(join(ROOT, 'scripts', 'narration.json'), 'utf8'));
  mkdirSync(SEGMENTS_DIR, { recursive: true });

  const scenes = [];
  const segments = [];
  const cursor = { value: 0 };

  for (const [offset, scene] of script.scenes.entries()) {
    const index = offset + 1;
    const target = join(SEGMENTS_DIR, `seg-${String(index).padStart(2, '0')}.mp3`);
    const words = await synthesize(scene.text, script.voice, script.rate, target);

    const spoken = round(probeDuration(target));
    const start = round(cursor.value);
    const duration = round(LEAD_IN_SECONDS + spoken + TAIL_SECONDS);

    scenes.push({
      id: scene.id,
      index,
      kicker: scene.kicker,
      title: scene.title,
      text: scene.text,
      audio: `audio/segments/seg-${String(index).padStart(2, '0')}.mp3`,
      start,
      duration,
      audioStart: round(start + LEAD_IN_SECONDS),
      audioDuration: spoken,
      highlights: resolveHighlights(scene, words, spoken),
      words: words.map((word) => ({ ...word, start: round(word.start), duration: round(word.duration) })),
    });
    segments.push(target);
    cursor.value = round(start + duration);
  }

  concatenate(segments, join(AUDIO_DIR, 'narration.mp3'));

  const timings = { total: round(cursor.value), leadIn: LEAD_IN_SECONDS, tail: TAIL_SECONDS, scenes };
  writeFileSync(join(AUDIO_DIR, 'timings.json'), `${JSON.stringify(timings, null, 2)}\n`, 'utf8');

  console.log(`Escenas: ${scenes.length}  Duración total: ${timings.total.toFixed(1)}s`);
  for (const scene of scenes) {
    console.log(
      `  ${scene.start.toFixed(2).padStart(7)}s +${scene.duration.toFixed(2).padStart(5)}s  ` +
        `${scene.id}  (${scene.highlights.length} resaltados)`,
    );
  }
}

await main();
