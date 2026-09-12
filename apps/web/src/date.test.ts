import { describe, expect, it } from 'vitest';
import { formatDuration, localIsoDate, prettyDate, reportRange } from './date';

describe('date helpers', () => {
  it('formats tracked time', () => expect(formatDuration(3725)).toBe('01:02:05'));
  it('returns a Monday-to-Sunday week', () => {
    const range = reportRange('week', new Date('2026-09-12T12:00:00'));
    expect(range).toEqual({ from: '2026-09-07', to: '2026-09-13' });
  });
  it('formats a local ISO date', () => expect(localIsoDate(new Date(2026, 0, 2, 12))).toBe('2026-01-02'));
  it('does not throw while report dates are loading', () => {
    expect(prettyDate('')).toBe('—');
    expect(prettyDate('not-a-date')).toBe('—');
  });
});
