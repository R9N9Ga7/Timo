using Microsoft.EntityFrameworkCore;

namespace Timo.Api;

public sealed class OccurrenceService(AppDbContext db)
{
    public static bool OccursOn(TaskDefinition task, DateOnly date)
    {
        if (date < task.StartDate || (task.ArchivedOn is not null && date >= task.ArchivedOn)) return false;
        var days = date.DayNumber - task.StartDate.DayNumber;
        return task.ScheduleType switch
        {
            ScheduleType.Daily => true,
            ScheduleType.IntervalDays => days % task.IntervalDays!.Value == 0,
            ScheduleType.Weekly => date.DayOfWeek == task.Weekday,
            _ => false
        };
    }

    public async Task EnsureAsync(Guid profileId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) return;
        var tasks = await db.Tasks.Where(x => x.ProfileId == profileId).ToListAsync(ct);

        foreach (var task in tasks)
        for (var day = from; day <= to; day = day.AddDays(1))
            if (OccursOn(task, day))
                // Today and reporting are intentionally loaded in parallel. Let SQLite arbitrate
                // concurrent attempts so occurrence generation remains safe for every API client.
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT OR IGNORE INTO Occurrences
                        (Id, TaskDefinitionId, ScheduledDate, PlannedSeconds, CompletedAtUtc)
                    VALUES
                        ({Guid.NewGuid()}, {task.Id}, {day}, {task.TargetSeconds}, NULL)
                    """, ct);
    }

    public static long ElapsedSeconds(TaskOccurrence occurrence, DateTime nowUtc) => occurrence.Sessions.Sum(session =>
        Math.Max(0L, (long)((session.EndedAtUtc ?? nowUtc) - session.StartedAtUtc).TotalSeconds));

    public async Task MaterializeThroughTodayAsync(TaskDefinition task, DateOnly today, CancellationToken ct = default)
    {
        await EnsureAsync(task.ProfileId, task.StartDate, today, ct);
    }
}
