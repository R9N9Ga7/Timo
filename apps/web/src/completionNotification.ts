export type CompletionNotificationDismissal = 'automatic' | 'manual';

const dismissalStorageKey = 'timo-completion-notification-dismissal';
const defaultDismissal: CompletionNotificationDismissal = 'automatic';

export function getCompletionNotificationDismissal(): CompletionNotificationDismissal {
  if (typeof window === 'undefined') return defaultDismissal;
  return window.localStorage.getItem(dismissalStorageKey) === 'manual' ? 'manual' : defaultDismissal;
}

export function setCompletionNotificationDismissal(value: CompletionNotificationDismissal) {
  if (typeof window !== 'undefined') window.localStorage.setItem(dismissalStorageKey, value);
  return value;
}
