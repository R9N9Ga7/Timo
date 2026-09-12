using System.ComponentModel.DataAnnotations;

namespace Timo.Api;

public enum ScheduleType { Daily, IntervalDays, Weekly }

public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(80)] public required string Name { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    [MaxLength(80)] public required string Name { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Profile? Profile { get; set; }
}

public sealed class TaskDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public Guid CategoryId { get; set; }
    [MaxLength(140)] public required string Title { get; set; }
    public int TargetSeconds { get; set; }
    public DateOnly StartDate { get; set; }
    public ScheduleType ScheduleType { get; set; }
    public int? IntervalDays { get; set; }
    public DayOfWeek? Weekday { get; set; }
    public DateOnly? ArchivedOn { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Category? Category { get; set; }
}

public sealed class TaskOccurrence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskDefinitionId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public int PlannedSeconds { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public TaskDefinition? TaskDefinition { get; set; }
    public List<TimeSession> Sessions { get; set; } = [];
}

public sealed class TimeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskOccurrenceId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public TaskOccurrence? TaskOccurrence { get; set; }
}
