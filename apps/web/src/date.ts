import type { RangeMode } from './types';

export const localIsoDate = (date = new Date()) => {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 10);
};

export function reportRange(mode: RangeMode, now = new Date()) {
  const from = new Date(now);
  const to = new Date(now);
  if (mode === 'week') {
    const mondayOffset = (now.getDay() + 6) % 7;
    from.setDate(now.getDate() - mondayOffset);
    to.setDate(from.getDate() + 6);
  } else if (mode === 'month') {
    from.setDate(1);
    to.setMonth(from.getMonth() + 1, 0);
  } else {
    from.setMonth(0, 1);
    to.setMonth(11, 31);
  }
  return { from: localIsoDate(from), to: localIsoDate(to) };
}

export const formatDuration = (seconds: number, compact = false) => {
  const total = Math.max(0, Math.floor(seconds));
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const secs = total % 60;
  if (compact) return hours ? `${hours}h ${minutes}m` : `${minutes}m`;
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
};

export const prettyDate = (iso?: string | null) => {
  if (!iso || !/^\d{4}-\d{2}-\d{2}$/.test(iso)) return '—';
  const date = new Date(`${iso}T12:00:00`);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat(undefined, {
    month: 'short', day: 'numeric', year: 'numeric'
  }).format(date);
};
