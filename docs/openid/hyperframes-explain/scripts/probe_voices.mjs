/**
 * Sonda de voces: sintetiza la MISMA frase del guion con varias voces en
 * español y mide duración, ritmo (chars/s), entonación (percentiles de F0) y
 * pausas, para elegir la voz más natural con datos en lugar de a ojo.
 *
 * El servicio de Edge solo admite un catálogo reducido de voces en consumer:
 * las que rechaza se reportan como RECHAZADO (no falla la sonda), y un turno
 * que no llega se reintenta antes de rendirse.
 *
 * Salida: audio/voice-samples/<voz>.mp3 (muestras escuchables, ignoradas por
 * git) + <voz>.json con el informe + comparativa.mp3 (todas las voces seguidas,
 * en el orden de la tabla) + tabla por stdout.
 *
 * Uso:  node scripts/probe_voices.mjs [id-de-voz]
 *
 * Los candidatos aceptan `rate` y `pitch` (fracción: -0.15 → prosody "-15%") para
 * medir si el servicio respeta la prosodia declarada.
 */

import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { EdgeSpeechTTS } from '@lobehub/tts';
import WebSocket from 'ws';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const SAMPLES_DIR = join(ROOT, 'audio', 'voice-samples');
const RATE = -0.15;
const PROBE_TEXT =
  'El flujo principal es Authorization Code con PKCE. El cliente genera un code verifier y su challenge, y los envía al authorize endpoint.';
const PAUSE_THRESHOLD_SECONDS = 0.2;
const SYNTHESIS_TIMEOUT_MS = 20_000;
const SYNTHESIS_ATTEMPTS = 2;
const TICKS_PER_SECOND = 10_000_000n;

const EDGE_HANDSHAKE_HEADERS = {
  Origin: 'chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold',
  'User-Agent':
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0',
  Pragma: 'no-cache',
  'Cache-Control': 'no-cache',
  'Accept-Language': 'en-US,en;q=0.9',
};

const CANDIDATES = [
  { id: 'alvaro', voice: 'es-ES-AlvaroNeural', note: 'voz anterior' },
  { id: 'elvira', voice: 'es-ES-ElviraNeural' },
  { id: 'jorge-mx', voice: 'es-MX-JorgeNeural' },
  { id: 'dalia-mx', voice: 'es-MX-DaliaNeural' },
  { id: 'alonso-us', voice: 'es-US-AlonsoNeural' },
  { id: 'paloma-us', voice: 'es-US-PalomaNeural', note: 'voz actual' },
  { id: 'elena-ar', voice: 'es-AR-ElenaNeural' },
  { id: 'salome-co', voice: 'es-CO-SalomeNeural' },
  { id: 'camila-pe', voice: 'es-PE-CamilaNeural' },
  { id: 'alvaro-ritmo', voice: 'es-ES-AlvaroNeural', rate: 0, note: 'rate 0' },
  { id: 'elvira-ritmo', voice: 'es-ES-ElviraNeural', rate: 0, note: 'rate 0' },
  { id: 'paloma-ritmo', voice: 'es-US-PalomaNeural', rate: 0, note: 'rate 0' },
  { id: 'paloma-tono-01', voice: 'es-US-PalomaNeural', pitch: -0.01, note: 'pitch -1% (control)' },
  { id: 'paloma-tono-10', voice: 'es-US-PalomaNeural', pitch: -0.1, note: 'pitch -10%' },
  { id: 'paloma-tono-15', voice: 'es-US-PalomaNeural', pitch: -0.15, note: 'pitch -15%' },
  { id: 'paloma-tono-20', voice: 'es-US-PalomaNeural', pitch: -0.2, note: 'pitch -20%' },
  { id: 'paloma-tono-30', voice: 'es-US-PalomaNeural', pitch: -0.3, note: 'pitch -30%' },
];

const wordBoundaries = [];
const serviceLog = [];

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

async function synthesize(candidate, target) {
  wordBoundaries.length = 0;
  serviceLog.length = 0;
  const response = await Promise.race([
    EdgeSpeechTTS.createRequest({
      payload: {
        input: PROBE_TEXT,
        options: {
          voice: candidate.voice,
          rate: candidate.rate ?? RATE,
          ...(candidate.pitch ? { pitch: candidate.pitch } : {}),
        },
      },
    }),
    new Promise((resolve) => setTimeout(() => resolve(null), SYNTHESIS_TIMEOUT_MS)),
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

function f0Stats(file) {
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

values = sorted(v for v in (f0_at(p) for p in range(0, len(samples) - 960, 240)) if v and 60 < v < 500)
if not values:
    print('0 0 0 0')
else:
    def pct(p):
        return values[min(len(values) - 1, round((len(values) - 1) * p / 100))]
    p10, p50, p90 = pct(10), pct(50), pct(90)
    print(f'{p10:.1f} {p50:.1f} {p90:.1f} {(p90 - p10) / p50:.3f}')
`;
  const stdout = execFileSync('python3', ['-c', python, wav], { encoding: 'utf8' });
  rmSync(wav, { force: true });
  const [p10, p50, p90, range] = stdout.trim().split(/\s+/).map(Number);
  return { p10, p50, p90, range };
}

function countPauses(words) {
  let pauses = 0;
  for (let index = 1; index < words.length; index += 1) {
    const previous = words[index - 1];
    const gap = words[index].start - (previous.start + previous.duration);
    if (gap >= PAUSE_THRESHOLD_SECONDS) pauses += 1;
  }
  return pauses;
}

function concatenate(samples, target) {
  const listFile = join(SAMPLES_DIR, 'concat.txt');
  writeFileSync(listFile, samples.map((file) => `file '${file}'\n`).join(''), 'utf8');
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
  mkdirSync(SAMPLES_DIR, { recursive: true });
  const produced = [];

  const chars = PROBE_TEXT.length;
  console.log(`Frase: ${chars} caracteres\n`);
  console.log('voz                      dur     cps   F0p10   F0p50   F0p90  enton  pausas');

  for (const candidate of CANDIDATES) {
    if (process.argv[2] && candidate.id !== process.argv[2]) continue;
    const target = join(SAMPLES_DIR, `${candidate.id}.mp3`);
    let words = null;
    for (let attempt = 1; attempt <= SYNTHESIS_ATTEMPTS && !words; attempt += 1) {
      try {
        words = await synthesize(candidate, target);
      } catch (error) {
        serviceLog.push(String(error));
      }
    }
    if (!words) {
      console.log(`${candidate.id.padEnd(24)}  RECHAZADO por el servicio`);
      for (const line of serviceLog) console.log(`    ${line}`);
      continue;
    }
    const duration = probeDuration(target);
    const f0 = f0Stats(target);
    const pauses = countPauses(words);
    console.log(
      `${candidate.id.padEnd(24)}  ${duration.toFixed(2).padStart(5)}s  ` +
        `${(chars / duration).toFixed(1).padStart(5)}  ${f0.p10.toFixed(0).padStart(5)}Hz ` +
        `${f0.p50.toFixed(0).padStart(6)}Hz ${f0.p90.toFixed(0).padStart(6)}Hz ` +
        `${f0.range.toFixed(2).padStart(6)}  ${String(pauses).padStart(4)}`,
    );
    writeFileSync(
      join(SAMPLES_DIR, `${candidate.id}.json`),
      `${JSON.stringify({ ...candidate, rate: candidate.rate ?? RATE, duration, cps: chars / duration, f0, pauses, words }, null, 2)}\n`,
      'utf8',
    );
    produced.push({ id: candidate.id, file: target });
  }

  if (produced.length > 1) {
    concatenate(
      produced.map((sample) => sample.file),
      join(SAMPLES_DIR, 'comparativa.mp3'),
    );
    console.log(`\ncomparativa.mp3, en este orden: ${produced.map((sample) => sample.id).join(' → ')}`);
  }

  console.log(`\nMP3 e informes en ${SAMPLES_DIR}`);
  const guide = JSON.parse(readFileSync(join(ROOT, 'scripts', 'narration.json'), 'utf8'));
  console.log(`Guion actual: voz ${guide.voice}, rate ${guide.rate}`);
}

await main();

