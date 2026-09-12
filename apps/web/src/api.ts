import type { Category, Occurrence, Profile, Report, Task, TaskInput } from './types';

const baseUrl = window.timoDesktop?.apiUrl || 'http://127.0.0.1:5127';
const token = window.timoDesktop?.apiToken;

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { 'X-Timo-Token': token } : {}),
      ...init?.headers
    }
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error || body.title || `Request failed (${response.status})`);
  }
  if (response.status === 204) return undefined as T;
  return response.json();
}

const q = (values: Record<string, string | undefined>) => new URLSearchParams(
  Object.entries(values).filter((entry): entry is [string, string] => Boolean(entry[1]))
).toString();

export const api = {
  profiles: () => request<Profile[]>('/api/profiles/'),
  createProfile: (name: string) => request<Profile>('/api/profiles/', { method: 'POST', body: JSON.stringify({ name }) }),
  renameProfile: (id: string, name: string) => request<Profile>(`/api/profiles/${id}`, { method: 'PUT', body: JSON.stringify({ name }) }),
  deleteProfile: (id: string) => request<void>(`/api/profiles/${id}`, { method: 'DELETE' }),
  categories: (profileId: string) => request<Category[]>(`/api/categories/?${q({ profileId })}`),
  createCategory: (profileId: string, name: string) => request<Category>('/api/categories/', { method: 'POST', body: JSON.stringify({ profileId, name }) }),
  renameCategory: (id: string, name: string) => request<Category>(`/api/categories/${id}`, { method: 'PUT', body: JSON.stringify({ name }) }),
  archiveCategory: (id: string) => request<void>(`/api/categories/${id}/archive`, { method: 'POST' }),
  tasks: (profileId: string) => request<Task[]>(`/api/tasks/?${q({ profileId })}`),
  createTask: (input: TaskInput) => request<{ id: string }>('/api/tasks/', { method: 'POST', body: JSON.stringify(input) }),
  updateTask: (id: string, input: TaskInput) => request<void>(`/api/tasks/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  archiveTask: (id: string) => request<void>(`/api/tasks/${id}/archive`, { method: 'POST' }),
  today: (profileId: string, date: string) => request<Occurrence[]>(`/api/today?${q({ profileId, date })}`),
  startTimer: (profileId: string, occurrenceId: string) => request<void>('/api/timer/start', { method: 'POST', body: JSON.stringify({ profileId, occurrenceId }) }),
  stopTimer: (profileId: string) => request<void>(`/api/timer/stop?${q({ profileId })}`, { method: 'POST' }),
  report: (profileId: string, from: string, to: string, categoryId?: string) => request<Report>(`/api/reports?${q({ profileId, from, to, categoryId })}`)
};
