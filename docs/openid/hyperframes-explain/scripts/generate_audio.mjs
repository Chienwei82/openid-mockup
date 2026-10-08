/**
 * Narración TTS por escena con @lobehub/tts (voces neuronales de Microsoft Edge).
 *
 * Salida:
 *   audio/segments/seg-NN.mp3   un clip de audio por escena
 *   audio/narration.mp3         narración completa (solo para revisión)
 *   audio/timings.json          ventanas medidas de cada escena
 *
 * Cada escena reserva un lead-in visual (la tarjeta aparece antes de que hable
 * la voz) y un tail de respiro antes del corte siguiente.
 *
 * Sobre `rate`: @lobehub/tts lo traduce a `prosody rate="<rate*100>%"` y el
 * servicio lo aplica de forma comprimida (y lo recorta por encima de 1). Con
 * `rate: 0.3` la narración queda en ~19 caracteres/s (~190 palabras/min), un
 * ritmo natural de presentador; con `rate: 1` se dispara a ~29 caracteres/s.
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
const TAIL_SECONDS = 0.7;

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

globalThis.WebSocket = class NodeEdgeWebSocket extends WebSocket {
  constructor(url, protocols) {
    super(url, protocols, { headers: EDGE_HANDSHAKE_HEADERS });
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
  const response = await EdgeSpeechTTS.createRequest({
    payload: { input: text, options: { voice, rate } },
  });
  writeFileSync(target, Buffer.from(await response.arrayBuffer()));
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
    await synthesize(scene.text, script.voice, script.rate, target);

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
    });
    segments.push(target);
    cursor.value = round(start + duration);
  }

  concatenate(segments, join(AUDIO_DIR, 'narration.mp3'));

  const timings = { total: round(cursor.value), leadIn: LEAD_IN_SECONDS, tail: TAIL_SECONDS, scenes };
  writeFileSync(join(AUDIO_DIR, 'timings.json'), `${JSON.stringify(timings, null, 2)}\n`, 'utf8');

  console.log(`Escenas: ${scenes.length}  Duración total: ${timings.total.toFixed(1)}s`);
  for (const scene of scenes) {
    console.log(`  ${scene.start.toFixed(2).padStart(7)}s +${scene.duration.toFixed(2).padStart(5)}s  ${scene.id}`);
  }
}

await main();
