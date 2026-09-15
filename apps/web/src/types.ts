export type ScheduleType = 'Daily' | 'IntervalDays' | 'Weekly';
export type RangeMode = 'week' | 'month' | 'year';

export interface Profile { id: string; name: string; createdAtUtc: string }
export interface Category { id: string; name: string; isArchived: boolean }
export interface Task {
  id: string; categoryId: string; categoryName: string; title: string; targetSeconds: number;
  startDate: string; scheduleType: ScheduleType; intervalDays?: number; weekday?: number; isArchived: boolean;
}
export interface Occurrence {
  id: string; taskId: string; title: string; categoryId: string; categoryName: string; date: string;
  plannedSeconds: number; elapsedSeconds: number; isComplete: boolean; isRunning: boolean; runningSinceUtc?: string;
}
export interface CategorySummary {
  categoryId: string; categoryName: string; plannedSeconds: number; actualSeconds: number;
  scheduledCount: number; completedCount: number;
}
export interface RoutineSummary {
  routineId: string; title: string; categoryId: string; categoryName: string;
  plannedSeconds: number; actualSeconds: number; scheduledCount: number; completedCount: number;
}
export interface DayTask { title: string; categoryName: string; actualSeconds: number; isComplete: boolean }
export interface DaySummary { date: string; actualSeconds: number; tasks: DayTask[] }
export interface RecentCompletion {
  occurrenceId: string; title: string; categoryName: string; date: string; actualSeconds: number; completedAtUtc: string;
}
export interface Report {
  from: string; to: string; plannedSeconds: number; actualSeconds: number; scheduledCount: number;
  completedCount: number; categories: CategorySummary[]; routines: RoutineSummary[]; days: DaySummary[];
  recentCompletions: RecentCompletion[];
}
export interface TaskInput {
  profileId: string; categoryId: string; title: string; targetSeconds: number; startDate: string;
  scheduleType: ScheduleType; intervalDays?: number; weekday?: number;
}
