namespace Timo.Api;

public sealed record NameRequest(string Name);
public sealed record CategoryRequest(Guid ProfileId, string Name);
public sealed record TaskRequest(Guid ProfileId, Guid CategoryId, string Title, int TargetSeconds,
    DateOnly StartDate, ScheduleType ScheduleType, int? IntervalDays, DayOfWeek? Weekday);
public sealed record StartTimerRequest(Guid ProfileId, Guid OccurrenceId);

public sealed record CategoryDto(Guid Id, string Name, bool IsArchived);
public sealed record TaskDto(Guid Id, Guid CategoryId, string CategoryName, string Title, int TargetSeconds,
    DateOnly StartDate, ScheduleType ScheduleType, int? IntervalDays, DayOfWeek? Weekday, bool IsArchived);
public sealed record OccurrenceDto(Guid Id, Guid TaskId, string Title, Guid CategoryId, string CategoryName,
    DateOnly Date, int PlannedSeconds, long ElapsedSeconds, bool IsComplete, bool IsRunning, DateTime? RunningSinceUtc);
public sealed record ActiveTimerDto(Guid SessionId, Guid OccurrenceId, DateTime StartedAtUtc);
public sealed record CategorySummaryDto(Guid CategoryId, string CategoryName, long PlannedSeconds, long ActualSeconds,
    int ScheduledCount, int CompletedCount);
public sealed record RoutineSummaryDto(Guid RoutineId, string Title, Guid CategoryId, string CategoryName,
    long PlannedSeconds, long ActualSeconds, int ScheduledCount, int CompletedCount);
public sealed record DayTaskDto(string Title, string CategoryName, long ActualSeconds, bool IsComplete);
public sealed record DaySummaryDto(DateOnly Date, long ActualSeconds, IReadOnlyList<DayTaskDto> Tasks);
public sealed record RecentCompletionDto(Guid OccurrenceId, string Title, string CategoryName, DateOnly Date,
    long ActualSeconds, DateTime CompletedAtUtc);
public sealed record ReportDto(DateOnly From, DateOnly To, long PlannedSeconds, long ActualSeconds,
    int ScheduledCount, int CompletedCount, IReadOnlyList<CategorySummaryDto> Categories,
    IReadOnlyList<RoutineSummaryDto> Routines, IReadOnlyList<DaySummaryDto> Days,
    IReadOnlyList<RecentCompletionDto> RecentCompletions);

public sealed record ProfileBackupDto(Guid Id, string Name, DateTime CreatedAtUtc);
public sealed record CategoryBackupDto(Guid Id, Guid ProfileId, string Name, bool IsArchived, DateTime CreatedAtUtc);
public sealed record TaskBackupDto(Guid Id, Guid ProfileId, Guid CategoryId, string Title, int TargetSeconds,
    DateOnly StartDate, ScheduleType ScheduleType, int? IntervalDays, DayOfWeek? Weekday,
    DateOnly? ArchivedOn, DateTime CreatedAtUtc);
public sealed record OccurrenceBackupDto(Guid Id, Guid TaskDefinitionId, DateOnly ScheduledDate,
    int PlannedSeconds, DateTime? CompletedAtUtc);
public sealed record SessionBackupDto(Guid Id, Guid TaskOccurrenceId, DateTime StartedAtUtc, DateTime? EndedAtUtc);
public sealed record DataBackupDto(int SchemaVersion, DateTime ExportedAtUtc,
    IReadOnlyList<ProfileBackupDto> Profiles, IReadOnlyList<CategoryBackupDto> Categories,
    IReadOnlyList<TaskBackupDto> Tasks, IReadOnlyList<OccurrenceBackupDto> Occurrences,
    IReadOnlyList<SessionBackupDto> Sessions);
