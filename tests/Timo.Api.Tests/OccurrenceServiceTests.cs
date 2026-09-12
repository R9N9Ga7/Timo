using Timo.Api;
using Xunit;

namespace Timo.Api.Tests;

public sealed class OccurrenceServiceTests
{
    [Fact]
    public void Daily_occurs_from_start_until_archive()
    {
        var task = MakeTask(ScheduleType.Daily, new DateOnly(2026, 9, 10));
        task.ArchivedOn = new DateOnly(2026, 9, 13);
        Assert.False(OccurrenceService.OccursOn(task, new DateOnly(2026, 9, 9)));
        Assert.True(OccurrenceService.OccursOn(task, new DateOnly(2026, 9, 12)));
        Assert.False(OccurrenceService.OccursOn(task, new DateOnly(2026, 9, 13)));
    }

    [Fact]
    public void Interval_is_anchored_across_months()
    {
        var task = MakeTask(ScheduleType.IntervalDays, new DateOnly(2026, 1, 30));
        task.IntervalDays = 3;
        Assert.True(OccurrenceService.OccursOn(task, new DateOnly(2026, 2, 2)));
        Assert.True(OccurrenceService.OccursOn(task, new DateOnly(2026, 2, 5)));
        Assert.False(OccurrenceService.OccursOn(task, new DateOnly(2026, 2, 6)));
    }

    [Fact]
    public void Interval_handles_leap_day()
    {
        var task = MakeTask(ScheduleType.IntervalDays, new DateOnly(2028, 2, 27));
        task.IntervalDays = 2;
        Assert.True(OccurrenceService.OccursOn(task, new DateOnly(2028, 2, 29)));
        Assert.True(OccurrenceService.OccursOn(task, new DateOnly(2028, 3, 2)));
    }

    [Fact]
    public void Weekly_uses_selected_weekday()
    {
        var task = MakeTask(ScheduleType.Weekly, new DateOnly(2026, 9, 12));
        task.Weekday = DayOfWeek.Monday;
        Assert.True(OccurrenceService.OccursOn(task, new DateOnly(2026, 9, 14)));
        Assert.False(OccurrenceService.OccursOn(task, new DateOnly(2026, 9, 15)));
    }

    [Fact]
    public void Elapsed_includes_closed_and_open_sessions()
    {
        var now = new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc);
        var occurrence = new TaskOccurrence { TaskDefinitionId = Guid.NewGuid(), ScheduledDate = new DateOnly(2026, 9, 12), PlannedSeconds = 1200 };
        occurrence.Sessions.Add(new TimeSession { StartedAtUtc = now.AddMinutes(-30), EndedAtUtc = now.AddMinutes(-20) });
        occurrence.Sessions.Add(new TimeSession { StartedAtUtc = now.AddMinutes(-5) });
        Assert.Equal(900, OccurrenceService.ElapsedSeconds(occurrence, now));
    }

    private static TaskDefinition MakeTask(ScheduleType type, DateOnly start) => new()
    {
        ProfileId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Title = "Study",
        TargetSeconds = 1800, StartDate = start, ScheduleType = type
    };
}
