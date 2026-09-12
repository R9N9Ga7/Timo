import { beforeEach, describe, expect, it } from 'vitest';
import { getCompletionNotificationDismissal, setCompletionNotificationDismissal } from './completionNotification';

describe('completion notification preference', () => {
  beforeEach(() => localStorage.clear());

  it('defaults to automatic dismissal', () => {
    expect(getCompletionNotificationDismissal()).toBe('automatic');
  });

  it('persists the dismissal preference', () => {
    expect(setCompletionNotificationDismissal('manual')).toBe('manual');
    expect(getCompletionNotificationDismissal()).toBe('manual');
    expect(setCompletionNotificationDismissal('automatic')).toBe('automatic');
    expect(getCompletionNotificationDismissal()).toBe('automatic');
  });

  it('falls back safely when storage contains an unknown value', () => {
    localStorage.setItem('timo-completion-notification-dismissal', 'unknown');
    expect(getCompletionNotificationDismissal()).toBe('automatic');
  });
});
