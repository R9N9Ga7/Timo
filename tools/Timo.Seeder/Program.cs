using Microsoft.EntityFrameworkCore;
using Timo.Api;

const string DemoProfileName = "Timo Demo";
var options = SeedOptions.Parse(args);
Directory.CreateDirectory(options.DataDirectory);
var databasePath = Path.Combine(options.DataDirectory, "timo.db");
var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite($"Data Source={databasePath}")
    .Options;

await using var db = new AppDbContext(dbOptions);
await db.Database.EnsureCreatedAsync();

var existing = await db.Profiles.SingleOrDefaultAsync(x => x.Name == DemoProfileName);
if (existing is not null && !options.Replace)
{
    Console.WriteLine($"The '{DemoProfileName}' profile already exists.");
    Console.WriteLine("Run npm.cmd run seed:replace to regenerate only its demo data.");
    return;
}

if (existing is not null)
{
    await db.Sessions.Where(x => x.TaskOccurrence!.TaskDefinition!.ProfileId == existing.Id).ExecuteDeleteAsync();
    await db.Occurrences.Where(x => x.TaskDefinition!.ProfileId == existing.Id).ExecuteDeleteAsync();
    await db.Tasks.Where(x => x.ProfileId == existing.Id).ExecuteDeleteAsync();
    await db.Categories.Where(x => x.ProfileId == existing.Id).ExecuteDeleteAsync();
    await db.Profiles.Where(x => x.Id == existing.Id).ExecuteDeleteAsync();
}

var today = DateOnly.FromDateTime(DateTime.Now);
var firstDay = today.AddDays(-(options.Days - 1));
var profile = new Profile
{
    Name = DemoProfileName,
    CreatedAtUtc = LocalToUtc(firstDay, 8, 0)
};
var categories = new[]
{
    new Category { ProfileId = profile.Id, Name = "Japanese", CreatedAtUtc = profile.CreatedAtUtc },
    new Category { ProfileId = profile.Id, Name = "Health", CreatedAtUtc = profile.CreatedAtUtc },
    new Category { ProfileId = profile.Id, Name = "Reading", CreatedAtUtc = profile.CreatedAtUtc },
    new Category { ProfileId = profile.Id, Name = "Mindfulness", CreatedAtUtc = profile.CreatedAtUtc },
    new Category { ProfileId = profile.Id, Name = "Music", CreatedAtUtc = profile.CreatedAtUtc, IsArchived = true }
};
var categoryByName = categories.ToDictionary(x => x.Name);
var archivedOn = today.AddDays(-60);
var taskSeeds = new[]
{
    new TaskSeed("Japanese", "Japanese listening", 30, ScheduleType.Daily, ActivityRate: .88),
    new TaskSeed("Japanese", "Kanji review", 20, ScheduleType.IntervalDays, IntervalDays: 2, ActivityRate: .82),
    new TaskSeed("Japanese", "Conversation practice", 45, ScheduleType.Weekly, Weekday: DayOfWeek.Saturday, ActivityRate: .72),
    new TaskSeed("Health", "Morning mobility", 15, ScheduleType.Daily, ActivityRate: .76),
    new TaskSeed("Health", "Strength training", 50, ScheduleType.Weekly, Weekday: DayOfWeek.Tuesday, ActivityRate: .68),
    new TaskSeed("Reading", "Read a chapter", 25, ScheduleType.Daily, ActivityRate: .70),
    new TaskSeed("Mindfulness", "Evening meditation", 10, ScheduleType.Daily, ActivityRate: .80),
    new TaskSeed("Music", "Piano practice", 35, ScheduleType.Weekly, Weekday: DayOfWeek.Sunday, ActivityRate: .64, ArchivedOn: archivedOn)
};

db.Profiles.Add(profile);
db.Categories.AddRange(categories);
var random = new Random(options.RandomSeed);
var occurrenceCount = 0;
var completedCount = 0;
long trackedSeconds = 0;

foreach (var seed in taskSeeds)
{
    var task = new TaskDefinition
    {
        ProfileId = profile.Id,
        CategoryId = categoryByName[seed.Category].Id,
        Title = seed.Title,
        TargetSeconds = seed.TargetMinutes * 60,
        StartDate = firstDay,
        ScheduleType = seed.ScheduleType,
        IntervalDays = seed.IntervalDays,
        Weekday = seed.Weekday,
        ArchivedOn = seed.ArchivedOn,
        CreatedAtUtc = profile.CreatedAtUtc
    };
    db.Tasks.Add(task);

    for (var day = firstDay; day <= today; day = day.AddDays(1))
    {
        if (!OccurrenceService.OccursOn(task, day)) continue;
        var occurrence = new TaskOccurrence
        {
            TaskDefinitionId = task.Id,
            ScheduledDate = day,
            PlannedSeconds = task.TargetSeconds
        };
        db.Occurrences.Add(occurrence);
        occurrenceCount++;

        var chance = day == today ? Math.Min(seed.ActivityRate, .58) : seed.ActivityRate;
        if (random.NextDouble() > chance) continue;

        var reachesGoal = random.NextDouble() < .78;
        var ratio = reachesGoal
            ? .98 + random.NextDouble() * .62
            : .22 + random.NextDouble() * .68;
        var actualSeconds = Math.Max(60, (int)Math.Round(task.TargetSeconds * ratio));
        var sessionCount = actualSeconds > 1800 && random.NextDouble() < .35 ? 2 : 1;
        var firstSessionSeconds = sessionCount == 2 ? actualSeconds / 2 : actualSeconds;
        var hour = random.Next(6, 20);
        var minute = random.Next(0, 4) * 15;
        var firstStart = LocalToUtc(day, hour, minute);
        var firstEnd = firstStart.AddSeconds(firstSessionSeconds);
        occurrence.Sessions.Add(new TimeSession
        {
            TaskOccurrenceId = occurrence.Id,
            StartedAtUtc = firstStart,
            EndedAtUtc = firstEnd
        });

        DateTime finalEnd = firstEnd;
        if (sessionCount == 2)
        {
            var secondStart = firstEnd.AddMinutes(random.Next(10, 45));
            finalEnd = secondStart.AddSeconds(actualSeconds - firstSessionSeconds);
            occurrence.Sessions.Add(new TimeSession
            {
                TaskOccurrenceId = occurrence.Id,
                StartedAtUtc = secondStart,
                EndedAtUtc = finalEnd
            });
        }

        trackedSeconds += actualSeconds;
        if (actualSeconds >= task.TargetSeconds)
        {
            occurrence.CompletedAtUtc = finalEnd;
            completedCount++;
        }
    }
}

await db.SaveChangesAsync();

Console.WriteLine($"Created '{DemoProfileName}' in {databasePath}");
Console.WriteLine($"Range:       {firstDay:yyyy-MM-dd} to {today:yyyy-MM-dd}");
Console.WriteLine($"Categories:  {categories.Length}");
Console.WriteLine($"Tasks:       {taskSeeds.Length}");
Console.WriteLine($"Occurrences: {occurrenceCount}");
Console.WriteLine($"Completed:   {completedCount}");
Console.WriteLine($"Tracked:     {TimeSpan.FromSeconds(trackedSeconds).ToString(@"d\.hh\:mm\:ss")}");

static DateTime LocalToUtc(DateOnly date, int hour, int minute) =>
    DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Local).ToUniversalTime();

internal sealed record TaskSeed(
    string Category,
    string Title,
    int TargetMinutes,
    ScheduleType ScheduleType,
    int? IntervalDays = null,
    DayOfWeek? Weekday = null,
    double ActivityRate = .75,
    DateOnly? ArchivedOn = null);

internal sealed record SeedOptions(string DataDirectory, int Days, int RandomSeed, bool Replace)
{
    public static SeedOptions Parse(string[] arguments)
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".timo-data"));
        var days = 365;
        var randomSeed = 20260912;
        var replace = false;

        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--replace":
                    replace = true;
                    break;
                case "--data-dir" when index + 1 < arguments.Length:
                    dataDirectory = Path.GetFullPath(arguments[++index]);
                    break;
                case "--days" when index + 1 < arguments.Length && int.TryParse(arguments[++index], out var value):
                    days = value;
                    break;
                case "--seed" when index + 1 < arguments.Length && int.TryParse(arguments[++index], out var value):
                    randomSeed = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown or incomplete option: {arguments[index]}");
            }
        }

        if (days is < 7 or > 730) throw new ArgumentOutOfRangeException(nameof(days), "Days must be between 7 and 730.");
        return new SeedOptions(dataDirectory, days, randomSeed, replace);
    }
}
