import { beforeEach, describe, expect, it } from 'vitest';
import { crossedTimerGoal, getTimerSignalVolume, setTimerSignalVolume } from './timerSignal';

describe('timer goal signal', () => {
  beforeEach(() => localStorage.clear());

  it('fires only when a running timer crosses its goal', () => {
    expect(crossedTimerGoal(59, 60, 60, true)).toBe(true);
    expect(crossedTimerGoal(60, 61, 60, true)).toBe(false);
    expect(crossedTimerGoal(59, 60, 60, false)).toBe(false);
  });

  it('does not fire for an occurrence first seen after completion', () => {
    expect(crossedTimerGoal(undefined, 90, 60, true)).toBe(false);
  });

  it('persists and clamps the shared signal volume', () => {
    expect(getTimerSignalVolume()).toBe(0.65);
    expect(setTimerSignalVolume(0.4)).toBe(0.4);
    expect(getTimerSignalVolume()).toBe(0.4);
    expect(setTimerSignalVolume(2)).toBe(1);
    expect(setTimerSignalVolume(-1)).toBe(0);
  });
});
