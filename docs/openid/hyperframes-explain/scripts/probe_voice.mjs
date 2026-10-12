/**
 * Sonda de configuración de voz: mide cómo aplica el servicio Edge las
 * opciones de prosodia (`rate`, `pitch`, `style`) y las pausas SSML.
 *
 * Sintetiza la MISMA frase con varias configuraciones, mide duración y
 * frecuencia fundamental (F0) y captura las marcas `WordBoundary`, para
 * poder elegir ritmo y tono con datos en lugar de a ojo.
 *
 * Uso:  node scripts/probe_voice.mjs
 * Salida: /tmp/hf-voice-probe/*.mp3 + informe por stdout.
 */

import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

import { EdgeSpeechTTS } from '@lobehub/tts';
import WebSocket from 'ws';

const PROBE_DIR = '/tmp/hf-voice-probe';
const VOICE = 'es-US-PalomaNeural';
const PROBE_TEXT =
  'El cliente genera un código de verificación y lo envía al servidor para canjearlo por sus tokens de acceso.';

const EDGE_HANDSHAKE_HEADERS = {
  Origin: 'chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold',
  'User-Agent':
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0',
  Pragma: 'no-cache',
  'Cache-Control': 'no-cache',
  'Accept-Language': 'en-US,en;q=0.9',
};

const TICKS_PER_SECOND = 10_000_000n;

const wordBoundaries = [];

globalThis.WebSocket = class NodeEdgeWebSocket extends WebSocket {
  constructor(url, protocols) {
    super(url, protocols, { headers: EDGE_HANDSHAKE_HEADERS });
    this.addEventListener('message', (event) => {
      if (typeof event.data !== 'string') return;
      const separator = event.data.indexOf('\r\n\r\n');
      if (separator < 0) return;
      const head = event.data.slice(0, separator);
      if (!head.includes('Path:audio.metadata')) {
        serviceLog.push(event.data.slice(0, 240).replaceAll('\r\n', ' | '));
        return;
      }
      const body = JSON.parse(event.data.slice(separator + 4));
      for (const entry of body.Metadata ?? []) {
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

const COMMAS = 'El cliente genera un código de verificación, y lo envía al servidor, para canjearlo por sus tokens de acceso.';
const ELLIPSIS_MID = 'El cliente genera un código de verificación... y lo envía al servidor... para canjearlo por sus tokens de acceso.';
const TWO_SENTENCES = 'El cliente genera un código de verificación. Lo envía al servidor para canjearlo por sus tokens de acceso.';

const VARIANTS = [
  { id: 'base', options: { rate: -0.15 } },
  { id: 'punct-commas', options: { rate: -0.15 }, input: COMMAS },
  { id: 'punct-ellipsis-mid', options: { rate: -0.15 }, input: ELLIPSIS_MID },
  { id: 'punct-two-sentences', options: { rate: -0.15 }, input: TWO_SENTENCES },
  { id: 'pitch-15', options: { rate: -0.15, pitch: -0.15 } },
];

const serviceLog = [];

async function synthesize(variant, target) {
  wordBoundaries.length = 0;
  serviceLog.length = 0;
  const response = await Promise.race([
    EdgeSpeechTTS.createRequest({
      payload: { input: variant.input ?? PROBE_TEXT, options: { voice: VOICE, ...variant.options } },
    }),
    new Promise((resolve) => setTimeout(() => resolve(null), 20_000)),
  ]);
  if (!response) return null;
  writeFileSync(target, Buffer.from(await response.arrayBuffer()));
  return wordBoundaries.slice();
}

function probeDuration(file) {
  const stdout = execFileSync(
    'ffprobe',
    ['-v', 'error', '-show_entries', 'format=duration', '-of', 'default=noprint_wrappers=1:nokey=1', file],
    { encoding: 'utf8' },
  );
  return Number.parseFloat(stdout.trim());
}

function medianF0(file) {
  const wav = file.replace(/\.mp3$/, '.wav');
  execFileSync('ffmpeg', ['-y', '-v', 'error', '-i', file, '-ar', '24000', '-ac', '1', '-f', 'wav', wav]);
  const python = String.raw`
import sys, wave, struct
wav = wave.open(sys.argv[1], 'rb')
rate = wav.getframerate()
frames = wav.readframes(wav.getnframes())
samples = struct.unpack('<%dh' % (len(frames) // 2), frames)

def f0_at(pos, win=960, maxlag=480):
    seg = samples[pos:pos + win]
    if len(seg) < win or max(abs(s) for s in seg) < 900:
        return None
    best, best_val = 0, 0.0
    for lag in range(40, maxlag):
        val = sum(seg[i] * seg[i + lag] for i in range(0, win - lag, 2))
        if val > best_val:
            best, best_val = lag, val
    return rate / best if best else None

values = [f0_at(p) for p in range(int(rate * 0.6), int(rate * 3.6), 480)]
values = [v for v in values if v and 60 < v < 500]
values.sort()
print(round(values[len(values) // 2], 1) if values else 0)
`;
  const stdout = execFileSync('python3', ['-c', python, wav], { encoding: 'utf8' });
  rmSync(wav, { force: true });
  return Number.parseFloat(stdout.trim());
}

async function main() {
  rmSync(PROBE_DIR, { recursive: true, force: true });
  mkdirSync(PROBE_DIR, { recursive: true });

  const chars = PROBE_TEXT.length;
  console.log(`Frase: ${chars} caracteres · voz ${VOICE}\n`);
  console.log('variante              dur     cps     F0    palabras marcadas');

  for (const variant of VARIANTS) {
    if (process.argv[2] && variant.id !== process.argv[2]) continue;
    const target = join(PROBE_DIR, `${variant.id}.mp3`);
    const words = await synthesize(variant, target);
    if (!words) {
      console.log(`${variant.id.padEnd(20)}  RECHAZADO por el servicio`);
      for (const line of serviceLog) console.log(`    ${line}`);
      continue;
    }
    const duration = probeDuration(target);
    const f0 = medianF0(target);
    const first = words[0];
    console.log(
      `${variant.id.padEnd(20)}  ${duration.toFixed(2).padStart(5)}s  ` +
        `${(chars / duration).toFixed(1).padStart(5)}  ${f0.toFixed(0).padStart(4)}Hz  ${words.length}` +
        (first ? `  (primera: "${first.text}" @${first.start.toFixed(2)}s)` : ''),
    );
    writeFileSync(
      join(PROBE_DIR, `${variant.id}.json`),
      `${JSON.stringify({ variant, duration, f0, words }, null, 2)}\n`,
      'utf8',
    );
  }

  console.log(`\nMP3 e informes JSON en ${PROBE_DIR}`);
  const samplePath = join(PROBE_DIR, 'base.json');
  try {
    const sample = JSON.parse(readFileSync(samplePath, 'utf8'));
    console.log(`Palabras marcadas (baseline): ${JSON.stringify(sample.words.slice(0, 6))}`);
  } catch {
    console.log('Sin muestra de palabras.');
  }
}

await main();
