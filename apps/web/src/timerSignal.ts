let audioContext: AudioContext | undefined;
const volumeStorageKey = 'timo-timer-signal-volume';
const defaultVolume = 0.65;
const maximumSignalGain = 0.15;

function clampVolume(volume: number) {
  return Number.isFinite(volume) ? Math.min(1, Math.max(0, volume)) : defaultVolume;
}

export function getTimerSignalVolume() {
  if (typeof window === 'undefined') return defaultVolume;
  const stored = window.localStorage.getItem(volumeStorageKey);
  return stored === null ? defaultVolume : clampVolume(Number(stored));
}

export function setTimerSignalVolume(volume: number) {
  const normalized = clampVolume(volume);
  if (typeof window !== 'undefined') window.localStorage.setItem(volumeStorageKey, String(normalized));
  return normalized;
}

function getAudioContext() {
  if (typeof window === 'undefined' || !window.AudioContext) return undefined;
  audioContext ??= new window.AudioContext();
  return audioContext;
}

export async function armTimerSignal() {
  const context = getAudioContext();
  if (context?.state === 'suspended') await context.resume();
}

export async function playTimerGoalSignal() {
  const context = getAudioContext();
  if (!context) return;
  if (context.state === 'suspended') await context.resume();
  const volume = getTimerSignalVolume();
  if (volume === 0) return;

  const start = context.currentTime;
  const gain = context.createGain();
  gain.gain.setValueAtTime(0.0001, start);
  gain.gain.exponentialRampToValueAtTime(maximumSignalGain * volume, start + 0.025);
  gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.48);
  gain.connect(context.destination);

  for (const [frequency, delay] of [[659.25, 0], [783.99, 0.15]] as const) {
    const oscillator = context.createOscillator();
    oscillator.type = 'sine';
    oscillator.frequency.setValueAtTime(frequency, start + delay);
    oscillator.connect(gain);
    oscillator.start(start + delay);
    oscillator.stop(start + delay + 0.3);
  }
}

export function crossedTimerGoal(previous: number | undefined, current: number, goal: number, isRunning: boolean) {
  return isRunning && previous !== undefined && previous < goal && current >= goal;
}
