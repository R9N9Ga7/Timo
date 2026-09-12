import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { api } from './api';
import { getCompletionNotificationDismissal, setCompletionNotificationDismissal, type CompletionNotificationDismissal } from './completionNotification';
import { formatDuration, localIsoDate, prettyDate, reportRange } from './date';
import { armTimerSignal, crossedTimerGoal, getTimerSignalVolume, playTimerGoalSignal, setTimerSignalVolume } from './timerSignal';
import type { Category, Occurrence, Profile, RangeMode, Report, ScheduleType, Task, TaskInput } from './types';

const emptyReport: Report = {
  from: '', to: '', plannedSeconds: 0, actualSeconds: 0, scheduledCount: 0,
  completedCount: 0, categories: [], days: [], recentCompletions: []
};
const logoUrl = `${import.meta.env.BASE_URL}logo.png`;

function Icon({ name }: { name: 'plus' | 'play' | 'stop' | 'settings' | 'clock' | 'chart' | 'tag' }) {
  const paths = {
    plus: <><path d="M12 5v14M5 12h14" /></>,
    play: <path d="m9 7 8 5-8 5V7Z" />,
    stop: <rect x="7" y="7" width="10" height="10" rx="2" />,
    settings: <><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1-2.8 2.8-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.6v.2h-4V21a1.7 1.7 0 0 0-1-1.6 1.7 1.7 0 0 0-1.9.3l-.1.1L4.2 17l.1-.1a1.7 1.7 0 0 0 .3-1.9A1.7 1.7 0 0 0 3 14H3v-4h.1a1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.9L4.2 7 7 4.2l.1.1a1.7 1.7 0 0 0 1.9.3A1.7 1.7 0 0 0 10 3V3h4v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.9-.3l.1-.1L19.8 7l-.1.1a1.7 1.7 0 0 0-.3 1.9 1.7 1.7 0 0 0 1.6 1h.2v4H21a1.7 1.7 0 0 0-1.6 1Z"/></>,
    clock: <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
    chart: <><path d="M4 19V9M10 19V5M16 19v-7M22 19H2"/></>,
    tag: <><path d="m20 13-7 7-9-9V4h7l9 9Z"/><circle cx="8.5" cy="8.5" r="1"/></>
  };
  return <svg className="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>{paths[name]}</svg>;
}

export default function App() {
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [profileId, setProfileId] = useState(localStorage.getItem('timo-profile') || '');
  const [categories, setCategories] = useState<Category[]>([]);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [today, setToday] = useState<Occurrence[]>([]);
  const [rangeMode, setRangeMode] = useState<RangeMode>('week');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [report, setReport] = useState<Report>(emptyReport);
  const [loadedAt, setLoadedAt] = useState(Date.now());
  const [tick, setTick] = useState(Date.now());
  const [modal, setModal] = useState<'task' | 'manage' | 'profile' | null>(null);
  const [editingTask, setEditingTask] = useState<Task | undefined>();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(true);
  const [notificationDismissal, setNotificationDismissal] = useState<CompletionNotificationDismissal>(getCompletionNotificationDismissal);
  const [completionNotification, setCompletionNotification] = useState<{ id: string; title: string }>();
  const previousElapsed = useRef(new Map<string, number>());

  const loadProfiles = useCallback(async () => {
    const values = await api.profiles();
    setProfiles(values);
    setProfileId(current => {
      const next = values.some(x => x.id === current) ? current : values[0]?.id || '';
      if (next) localStorage.setItem('timo-profile', next);
      return next;
    });
  }, []);

  const loadProfileData = useCallback(async () => {
    if (!profileId) return;
    const date = localIsoDate();
    const range = reportRange(rangeMode);
    const [categoryRows, taskRows, todayRows, reportData] = await Promise.all([
      api.categories(profileId), api.tasks(profileId), api.today(profileId, date),
      api.report(profileId, range.from, range.to, categoryFilter || undefined)
    ]);
    setCategories(categoryRows);
    setTasks(taskRows);
    setToday(todayRows);
    setReport(reportData);
    setLoadedAt(Date.now());
  }, [profileId, rangeMode, categoryFilter]);

  useEffect(() => {
    loadProfiles().catch(e => setError(e.message)).finally(() => setBusy(false));
  }, [loadProfiles]);
  useEffect(() => {
    setBusy(true);
    loadProfileData().catch(e => setError(e.message)).finally(() => setBusy(false));
  }, [loadProfileData]);
  useEffect(() => {
    const id = window.setInterval(() => setTick(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);
  useEffect(() => {
    if (!modal) return;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = previousOverflow; };
  }, [modal]);
  useEffect(() => {
    const visibleIds = new Set(today.map(item => item.id));
    for (const item of today) {
      const elapsed = item.elapsedSeconds + (item.isRunning ? Math.max(0, Math.floor((tick - loadedAt) / 1000)) : 0);
      const previous = previousElapsed.current.get(item.id);
      if (crossedTimerGoal(previous, elapsed, item.plannedSeconds, item.isRunning)) {
        void playTimerGoalSignal();
        setCompletionNotification({ id: item.id, title: item.title });
      }
      previousElapsed.current.set(item.id, elapsed);
    }
    for (const id of previousElapsed.current.keys()) {
      if (!visibleIds.has(id)) previousElapsed.current.delete(id);
    }
  }, [today, tick, loadedAt]);
  useEffect(() => {
    if (!completionNotification || notificationDismissal !== 'automatic') return;
    const id = window.setTimeout(() => setCompletionNotification(undefined), 6000);
    return () => window.clearTimeout(id);
  }, [completionNotification, notificationDismissal]);

  const currentProfile = profiles.find(x => x.id === profileId);
  const effectiveSeconds = (item: Occurrence) => item.elapsedSeconds + (item.isRunning ? Math.max(0, Math.floor((tick - loadedAt) / 1000)) : 0);
  const completedToday = today.filter(item => effectiveSeconds(item) >= item.plannedSeconds).length;

  async function toggleTimer(item: Occurrence) {
    try {
      setError('');
      if (item.isRunning) await api.stopTimer(profileId);
      else {
        await armTimerSignal();
        await api.startTimer(profileId, item.id);
      }
      await loadProfileData();
    } catch (e) { setError((e as Error).message); }
  }

  function openTask(task?: Task) {
    setEditingTask(task);
    setModal('task');
  }

  async function saved() {
    setModal(null);
    setEditingTask(undefined);
    await loadProfileData();
  }

  if (busy && profiles.length === 0) return <div className="center-state"><div className="loader"/><p>Opening your day…</p></div>;

  if (!profileId) return <Welcome onCreated={async name => { await api.createProfile(name); await loadProfiles(); }} error={error} />;

  return <div className="app-shell">
    <header className="topbar">
      <a className="brand" href="#top" aria-label="Timo home"><img className="brand-logo" src={logoUrl} alt=""/><span>timo</span></a>
      <nav className="nav-pills" aria-label="Main navigation"><a className="active" href="#today">Today</a><a href="#insights">Insights</a></nav>
      <div className="top-actions">
        <button className="profile-chip" onClick={() => setModal('profile')}><span className="avatar">{currentProfile?.name.slice(0, 1).toUpperCase()}</span>{currentProfile?.name}</button>
        <button className="icon-button" aria-label="Manage tasks and categories" onClick={() => setModal('manage')}><Icon name="settings" /></button>
      </div>
    </header>

    <main id="top">
      {error && <div className="error-banner"><span>{error}</span><button onClick={() => setError('')}>Dismiss</button></div>}
      <section className="hero">
        <div><p className="eyebrow">{new Intl.DateTimeFormat(undefined, { weekday: 'long', month: 'long', day: 'numeric' }).format(new Date())}</p><h1>Make time for<br/><em>what matters.</em></h1><p className="hero-copy">Small routines, thoughtfully repeated.</p></div>
        <button className="primary-button" onClick={() => openTask()}><Icon name="plus" />New routine</button>
      </section>

      <section className="today-section" id="today">
        <div className="section-heading"><div><span className="section-kicker">Your rhythm</span><h2>Today’s routines</h2></div><div className="completion-note"><strong>{completedToday}</strong> of {today.length} complete</div></div>
        {today.length === 0 ? <Empty title="A quiet day" text="Create a routine and choose today as its start date." action={() => openTask()} /> :
          <div className="task-grid">{today.map(item => {
            const elapsed = effectiveSeconds(item);
            const complete = elapsed >= item.plannedSeconds;
            const progress = Math.min(100, elapsed / item.plannedSeconds * 100);
            return <article className={`task-card ${complete ? 'complete' : ''} ${item.isRunning ? 'running' : ''}`} key={item.id}>
              <div className="task-card-top"><span className="category-label"><span />{item.categoryName}</span><button className="text-button" onClick={() => openTask(tasks.find(x => x.id === item.taskId))}>Edit</button></div>
              <h3>{item.title}</h3>
              <div className="timer-row">
                <div><div className="timer-value">{formatDuration(elapsed)}</div><div className="timer-goal">of {formatDuration(item.plannedSeconds, true)} planned</div></div>
                <button className={`timer-button ${item.isRunning ? 'stop' : ''}`} aria-label={item.isRunning ? 'Stop timer' : 'Start timer'} onClick={() => toggleTimer(item)}><Icon name={item.isRunning ? 'stop' : 'play'} /></button>
              </div>
              <div className="progress-track"><span style={{ width: `${progress}%` }} /></div>
              <div className="task-status"><span>{complete ? 'Goal reached' : item.isRunning ? 'In progress' : 'Ready when you are'}</span><span>{Math.round(progress)}%</span></div>
            </article>;
          })}</div>}
      </section>

      <section className="insights" id="insights">
        <div className="section-heading insight-heading"><div><span className="section-kicker">Look back</span><h2>Your progress</h2></div>
          <div className="filters"><select value={categoryFilter} onChange={e => setCategoryFilter(e.target.value)} aria-label="Filter category"><option value="">All categories</option>{categories.map(x => <option value={x.id} key={x.id}>{x.name}{x.isArchived ? ' (archived)' : ''}</option>)}</select>
          <div className="segmented">{(['week', 'month', 'year'] as RangeMode[]).map(mode => <button key={mode} className={rangeMode === mode ? 'active' : ''} onClick={() => setRangeMode(mode)}>{mode}</button>)}</div></div>
        </div>
        <div className="metric-grid">
          <Metric icon="clock" label="Time invested" value={formatDuration(report.actualSeconds, true)} note={`${formatDuration(report.plannedSeconds, true)} planned`} />
          <Metric icon="chart" label="Routines completed" value={`${report.completedCount}/${report.scheduledCount}`} note={`${report.scheduledCount ? Math.round(report.completedCount / report.scheduledCount * 100) : 0}% completion`} />
          <Metric icon="tag" label="Leading category" value={report.categories[0]?.categoryName || '—'} note={report.categories[0] ? formatDuration(report.categories[0].actualSeconds, true) : 'No tracked time'} />
        </div>

        <div className="insight-grid">
          <div className="panel category-panel"><div className="panel-heading"><div><span className="section-kicker">Time by focus</span><h3>Categories</h3></div><span>{prettyDate(report.from)} – {prettyDate(report.to)}</span></div>
            {report.categories.length === 0 ? <p className="muted">Track some time to see your focus take shape.</p> : <div className="category-bars">{report.categories.map((row, index) => {
              const max = Math.max(...report.categories.map(x => x.actualSeconds), 1);
              return <button key={row.categoryId} className="category-row" onClick={() => setCategoryFilter(row.categoryId)}>
                <span className={`category-dot tone-${index % 5}`} /><span className="category-name">{row.categoryName}<small>{row.completedCount} of {row.scheduledCount} routines</small></span>
                <span className="bar"><i style={{ width: `${row.actualSeconds / max * 100}%` }} /></span><strong>{formatDuration(row.actualSeconds, true)}</strong>
              </button>;
            })}</div>}
          </div>
          <div className="panel heatmap-panel"><div className="panel-heading"><div><span className="section-kicker">Daily consistency</span><h3>Activity map</h3></div><span>Hover for details</span></div><Heatmap report={report} /></div>
        </div>

        <div className="panel recent-panel"><div className="panel-heading"><div><span className="section-kicker">Recently finished</span><h3>Completed routines</h3></div></div>
          {report.recentCompletions.length === 0 ? <p className="muted">Completed routines will appear here.</p> : <div className="recent-list">{report.recentCompletions.map(item => <div className="recent-item" key={item.occurrenceId}><span className="check">✓</span><div><strong>{item.title}</strong><small>{item.categoryName} · {prettyDate(item.date)}</small></div><span>{formatDuration(item.actualSeconds, true)}</span></div>)}</div>}
        </div>
      </section>
    </main>

    {completionNotification && <div className="completion-toast" role="status" aria-live="polite">
      <span className="completion-toast-mark">✓</span><div><strong>Routine completed</strong><span>{completionNotification.title} reached its goal.</span></div>
      <button type="button" aria-label="Close completion notification" onClick={() => setCompletionNotification(undefined)}>×</button>
    </div>}

    {modal === 'task' && <TaskModal profileId={profileId} categories={categories} task={editingTask} onClose={() => setModal(null)} onSaved={saved} onCategoryCreated={async name => { const result = await api.createCategory(profileId, name); setCategories(await api.categories(profileId)); return result.id; }} />}
    {modal === 'manage' && <ManageModal profileId={profileId} categories={categories} tasks={tasks} notificationDismissal={notificationDismissal} onNotificationDismissalChange={value => { setNotificationDismissal(value); setCompletionNotificationDismissal(value); }} onClose={() => setModal(null)} onChanged={loadProfileData} onEdit={openTask} />}
    {modal === 'profile' && <ProfileModal profiles={profiles} activeId={profileId} onClose={() => setModal(null)} onSelect={id => { localStorage.setItem('timo-profile', id); setProfileId(id); setCategoryFilter(''); setModal(null); }} onChanged={loadProfiles} />}
  </div>;
}

function Welcome({ onCreated, error }: { onCreated: (name: string) => Promise<void>; error: string }) {
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  return <main className="welcome"><div className="welcome-art"><img className="welcome-logo" src={logoUrl} alt="Timo"/></div><div className="welcome-form"><span className="section-kicker">Welcome</span><h1>Your time,<br/><em>intentionally.</em></h1><p>Create a local profile to begin shaping the routines that matter to you.</p>{error && <p className="form-error">{error}</p>}<form onSubmit={async e => { e.preventDefault(); setBusy(true); await onCreated(name).finally(() => setBusy(false)); }}><label>Your name<input autoFocus required maxLength={80} value={name} onChange={e => setName(e.target.value)} placeholder="How should we call you?" /></label><button className="primary-button" disabled={busy}>{busy ? 'Creating…' : 'Begin'}</button></form></div></main>;
}

function Metric({ icon, label, value, note }: { icon: 'clock' | 'chart' | 'tag'; label: string; value: string; note: string }) {
  return <div className="metric"><span className="metric-icon"><Icon name={icon}/></span><div><small>{label}</small><strong>{value}</strong><span>{note}</span></div></div>;
}

function Empty({ title, text, action }: { title: string; text: string; action: () => void }) {
  return <div className="empty"><div className="empty-mark">○</div><h3>{title}</h3><p>{text}</p><button className="secondary-button" onClick={action}><Icon name="plus"/>Create a routine</button></div>;
}

function Modal({ title, subtitle, onClose, children, wide = false }: { title: string; subtitle?: string; onClose: () => void; children: React.ReactNode; wide?: boolean }) {
  return <div className="modal-backdrop" role="presentation" onMouseDown={e => e.target === e.currentTarget && onClose()}><section className={`modal ${wide ? 'wide' : ''}`} role="dialog" aria-modal="true" aria-label={title}><button className="modal-close" onClick={onClose}>×</button><span className="section-kicker">{subtitle}</span><h2>{title}</h2>{children}</section></div>;
}

function TaskModal({ profileId, categories, task, onClose, onSaved, onCategoryCreated }: { profileId: string; categories: Category[]; task?: Task; onClose: () => void; onSaved: () => Promise<void>; onCategoryCreated: (name: string) => Promise<string> }) {
  const activeCategories = categories.filter(x => !x.isArchived);
  const [title, setTitle] = useState(task?.title || '');
  const [categoryId, setCategoryId] = useState(task?.categoryId || activeCategories[0]?.id || '__new');
  const [minutes, setMinutes] = useState(task ? Math.round(task.targetSeconds / 60) : 30);
  const [startDate, setStartDate] = useState(task?.startDate || localIsoDate());
  const [scheduleType, setScheduleType] = useState<ScheduleType>(task?.scheduleType || 'Daily');
  const [intervalDays, setIntervalDays] = useState(task?.intervalDays || 2);
  const [weekday, setWeekday] = useState(task?.weekday ?? new Date().getDay());
  const [newCategory, setNewCategory] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError('');
    try {
      let chosen = categoryId;
      if (chosen === '__new') chosen = await onCategoryCreated(newCategory.trim());
      const input: TaskInput = { profileId, categoryId: chosen, title, targetSeconds: minutes * 60, startDate, scheduleType,
        ...(scheduleType === 'IntervalDays' ? { intervalDays } : {}), ...(scheduleType === 'Weekly' ? { weekday } : {}) };
      if (task) await api.updateTask(task.id, input); else await api.createTask(input);
      await onSaved();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  return <Modal title={task ? 'Edit routine' : 'Create a routine'} subtitle={task ? 'Shape what comes next' : 'A small promise to yourself'} onClose={onClose}><form className="form" onSubmit={submit}>
    {error && <p className="form-error">{error}</p>}
    <label>Routine name<input autoFocus required maxLength={140} value={title} onChange={e => setTitle(e.target.value)} placeholder="e.g. Japanese listening" /></label>
    <div className="form-row"><label>Category<select value={categoryId} onChange={e => setCategoryId(e.target.value)}>{activeCategories.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}<option value="__new">＋ New category</option></select></label><label>Minutes<input type="number" min="1" max="1440" required value={minutes} onChange={e => setMinutes(Number(e.target.value))} /></label></div>
    {categoryId === '__new' && <label>New category<input required value={newCategory} onChange={e => setNewCategory(e.target.value)} placeholder="e.g. Japanese" /></label>}
    <label>Starts on<input type="date" required value={startDate} onChange={e => setStartDate(e.target.value)} /></label>
    <fieldset><legend>Repeat</legend><div className="choice-grid">{([['Daily', 'Every day'], ['IntervalDays', 'Every few days'], ['Weekly', 'Once a week']] as [ScheduleType, string][]).map(([value, label]) => <label className={scheduleType === value ? 'selected' : ''} key={value}><input type="radio" name="schedule" value={value} checked={scheduleType === value} onChange={() => setScheduleType(value)} /><span>{label}</span></label>)}</div></fieldset>
    {scheduleType === 'IntervalDays' && <label>Repeat every <span className="inline-input"><input type="number" min="2" max="365" value={intervalDays} onChange={e => setIntervalDays(Number(e.target.value))} /> days</span></label>}
    {scheduleType === 'Weekly' && <label>Day of week<select value={weekday} onChange={e => setWeekday(Number(e.target.value))}>{['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'].map((day, index) => <option value={index} key={day}>{day}</option>)}</select></label>}
    <div className="modal-actions"><button type="button" className="ghost-button" onClick={onClose}>Cancel</button><button className="primary-button" disabled={busy}>{busy ? 'Saving…' : task ? 'Save changes' : 'Create routine'}</button></div>
  </form></Modal>;
}

function ManageModal({ profileId, categories, tasks, notificationDismissal, onNotificationDismissalChange, onClose, onChanged, onEdit }: { profileId: string; categories: Category[]; tasks: Task[]; notificationDismissal: CompletionNotificationDismissal; onNotificationDismissalChange: (value: CompletionNotificationDismissal) => void; onClose: () => void; onChanged: () => Promise<void>; onEdit: (task: Task) => void }) {
  const [name, setName] = useState('');
  const [error, setError] = useState('');
  const [signalVolume, setSignalVolume] = useState(() => Math.round(getTimerSignalVolume() * 100));
  async function run(action: () => Promise<unknown>) { try { setError(''); await action(); await onChanged(); } catch (e) { setError((e as Error).message); } }
  async function previewSignal() { try { setError(''); await armTimerSignal(); await playTimerGoalSignal(); } catch (e) { setError((e as Error).message); } }
  return <Modal title="Your routines" subtitle="Organize your practice" onClose={onClose} wide>{error && <p className="form-error">{error}</p>}<div className="manage-grid"><div><div className="manage-heading"><h3>Categories</h3><form onSubmit={e => { e.preventDefault(); run(() => api.createCategory(profileId, name)).then(() => setName('')); }}><input required value={name} onChange={e => setName(e.target.value)} placeholder="New category"/><button>+</button></form></div><div className="manage-list">{categories.map(category => <div key={category.id} className={category.isArchived ? 'archived' : ''}><span>{category.name}<small>{tasks.filter(x => x.categoryId === category.id && !x.isArchived).length} routines</small></span>{!category.isArchived && <span><button onClick={() => { const next = prompt('Rename category', category.name); if (next) run(() => api.renameCategory(category.id, next)); }}>Rename</button><button onClick={() => confirm(`Archive ${category.name}?`) && run(() => api.archiveCategory(category.id))}>Archive</button></span>}</div>)}</div></div>
    <div><div className="manage-heading"><h3>Tasks</h3></div><div className="manage-list">{tasks.map(task => <div key={task.id} className={task.isArchived ? 'archived' : ''}><span>{task.title}<small>{task.categoryName} · {formatDuration(task.targetSeconds, true)}</small></span>{!task.isArchived && <span><button onClick={() => onEdit(task)}>Edit</button><button onClick={() => confirm(`Archive ${task.title}?`) && run(() => api.archiveTask(task.id))}>Archive</button></span>}</div>)}</div></div></div>
    <section className="sound-settings"><div><span className="section-kicker">Timer notification</span><h3>Completion alert</h3><p>A notification and sound appear when any routine reaches its planned time.</p></div><div className="notification-controls"><div className="sound-control"><label htmlFor="signal-volume">Volume</label><input id="signal-volume" type="range" min="0" max="100" step="1" value={signalVolume} onChange={event => { const value = Number(event.target.value); setSignalVolume(value); setTimerSignalVolume(value / 100); }} /><output htmlFor="signal-volume">{signalVolume}%</output><button type="button" className="secondary-button" onClick={previewSignal}>Play sound</button></div><label className="dismissal-control" htmlFor="notification-dismissal"><span>Dismiss notification</span><select id="notification-dismissal" value={notificationDismissal} onChange={event => onNotificationDismissalChange(event.target.value as CompletionNotificationDismissal)}><option value="automatic">Automatically after 6 seconds</option><option value="manual">Only when I close it</option></select></label></div></section>
  </Modal>;
}

function ProfileModal({ profiles, activeId, onClose, onSelect, onChanged }: { profiles: Profile[]; activeId: string; onClose: () => void; onSelect: (id: string) => void; onChanged: () => Promise<void> }) {
  const [name, setName] = useState('');
  const [error, setError] = useState('');
  async function run(action: () => Promise<unknown>) { try { setError(''); await action(); await onChanged(); } catch (e) { setError((e as Error).message); } }
  return <Modal title="Local profiles" subtitle="Choose your space" onClose={onClose}>{error && <p className="form-error">{error}</p>}<div className="profile-list">{profiles.map(profile => <div className={profile.id === activeId ? 'selected' : ''} key={profile.id}><button className="profile-main" onClick={() => onSelect(profile.id)}><span className="avatar">{profile.name[0].toUpperCase()}</span>{profile.name}</button><span><button onClick={() => { const next = prompt('Rename profile', profile.name); if (next) run(() => api.renameProfile(profile.id, next)); }}>Rename</button>{profiles.length > 1 && <button onClick={() => confirm(`Delete ${profile.name} and all of its data?`) && run(() => api.deleteProfile(profile.id))}>Delete</button>}</span></div>)}</div><form className="add-profile" onSubmit={e => { e.preventDefault(); run(() => api.createProfile(name)).then(() => setName('')); }}><input required value={name} onChange={e => setName(e.target.value)} placeholder="New profile name"/><button className="secondary-button"><Icon name="plus"/>Add profile</button></form></Modal>;
}

function Heatmap({ report }: { report: Report }) {
  const days = useMemo(() => {
    if (!report.from || !report.to) return [];
    const data = new Map(report.days.map(x => [x.date, x]));
    const result = [];
    for (let date = new Date(`${report.from}T12:00:00`), end = new Date(`${report.to}T12:00:00`); date <= end; date.setDate(date.getDate() + 1)) {
      const iso = localIsoDate(date);
      result.push(data.get(iso) || { date: iso, actualSeconds: 0, tasks: [] });
    }
    return result;
  }, [report]);
  const max = Math.max(...days.map(x => x.actualSeconds), 1);
  return <div className="heatmap-wrap"><div className="weekday-labels"><span>M</span><span>W</span><span>F</span></div><div className="heatmap" style={{ gridTemplateColumns: `repeat(${Math.ceil((days.length + (days[0] ? (new Date(`${days[0].date}T12:00:00`).getDay() + 6) % 7 : 0)) / 7)}, 1fr)` }}>
    {days[0] && Array.from({ length: (new Date(`${days[0].date}T12:00:00`).getDay() + 6) % 7 }).map((_, i) => <span className="heat-cell blank" key={`blank-${i}`} />)}
    {days.map(day => { const level = day.actualSeconds ? Math.max(1, Math.ceil(day.actualSeconds / max * 4)) : 0; const details = day.tasks.length ? day.tasks.map(x => `${x.title} (${formatDuration(x.actualSeconds, true)})`).join('\n') : 'No time tracked'; return <span className={`heat-cell level-${level}`} key={day.date} title={`${prettyDate(day.date)} · ${formatDuration(day.actualSeconds, true)}\n${details}`} />; })}
  </div><div className="heat-legend"><span>Less</span>{[0,1,2,3,4].map(x => <i className={`heat-cell level-${x}`} key={x}/>)}<span>More</span></div></div>;
}
