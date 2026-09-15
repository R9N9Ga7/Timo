using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Timo.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
var dataDir = Environment.GetEnvironmentVariable("TIMO_DATA_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Timo");
Directory.CreateDirectory(dataDir);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "timo.db")}"));
builder.Services.AddScoped<OccurrenceService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod()
    .SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Host is "localhost" or "127.0.0.1")));

var app = builder.Build();
app.UseCors();
var token = Environment.GetEnvironmentVariable("TIMO_API_TOKEN");
app.Use(async (context, next) =>
{
    if (!string.IsNullOrEmpty(token) && context.Request.Path != "/health" && context.Request.Headers["X-Timo-Token"] != token)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    await next();
});

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var profiles = app.MapGroup("/api/profiles");
profiles.MapGet("/", async (AppDbContext db) => await db.Profiles.OrderBy(x => x.CreatedAtUtc).ToListAsync());
profiles.MapPost("/", async (NameRequest request, AppDbContext db) =>
{
    var name = request.Name.Trim();
    if (name.Length is < 1 or > 80) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name must be between 1 and 80 characters."] });
    var profile = new Profile { Name = name };
    db.Profiles.Add(profile);
    await db.SaveChangesAsync();
    return Results.Created($"/api/profiles/{profile.Id}", profile);
});
profiles.MapPut("/{id:guid}", async (Guid id, NameRequest request, AppDbContext db) =>
{
    var profile = await db.Profiles.FindAsync(id);
    if (profile is null) return Results.NotFound();
    var name = request.Name.Trim();
    if (name.Length is < 1 or > 80) return Results.BadRequest(new { error = "Name must be between 1 and 80 characters." });
    profile.Name = name;
    await db.SaveChangesAsync();
    return Results.Ok(profile);
});
profiles.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var profile = await db.Profiles.FindAsync(id);
    if (profile is null) return Results.NotFound();
    db.Profiles.Remove(profile);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

var categories = app.MapGroup("/api/categories");
categories.MapGet("/", async (Guid profileId, AppDbContext db) =>
    await db.Categories.Where(x => x.ProfileId == profileId).OrderBy(x => x.IsArchived).ThenBy(x => x.Name)
        .Select(x => new CategoryDto(x.Id, x.Name, x.IsArchived)).ToListAsync());
categories.MapPost("/", async (CategoryRequest request, AppDbContext db) =>
{
    var name = request.Name.Trim();
    if (name.Length is < 1 or > 80 || !await db.Profiles.AnyAsync(x => x.Id == request.ProfileId)) return Results.BadRequest(new { error = "A valid profile and category name are required." });
    if (await db.Categories.AnyAsync(x => x.ProfileId == request.ProfileId && x.Name.ToLower() == name.ToLower())) return Results.Conflict(new { error = "That category already exists." });
    var category = new Category { ProfileId = request.ProfileId, Name = name };
    db.Categories.Add(category);
    await db.SaveChangesAsync();
    return Results.Created($"/api/categories/{category.Id}", new CategoryDto(category.Id, category.Name, false));
});
categories.MapPut("/{id:guid}", async (Guid id, NameRequest request, AppDbContext db) =>
{
    var category = await db.Categories.FindAsync(id);
    if (category is null) return Results.NotFound();
    var name = request.Name.Trim();
    if (name.Length is < 1 or > 80) return Results.BadRequest(new { error = "Name must be between 1 and 80 characters." });
    if (await db.Categories.AnyAsync(x => x.ProfileId == category.ProfileId && x.Id != id && x.Name.ToLower() == name.ToLower())) return Results.Conflict(new { error = "That category already exists." });
    category.Name = name;
    await db.SaveChangesAsync();
    return Results.Ok(new CategoryDto(category.Id, category.Name, category.IsArchived));
});
categories.MapPost("/{id:guid}/archive", async (Guid id, AppDbContext db) =>
{
    var category = await db.Categories.FindAsync(id);
    if (category is null) return Results.NotFound();
    category.IsArchived = true;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

var tasks = app.MapGroup("/api/tasks");
tasks.MapGet("/", async (Guid profileId, AppDbContext db) => await db.Tasks.Include(x => x.Category)
    .Where(x => x.ProfileId == profileId).OrderBy(x => x.ArchivedOn != null).ThenBy(x => x.Title)
    .Select(x => new TaskDto(x.Id, x.CategoryId, x.Category!.Name, x.Title, x.TargetSeconds, x.StartDate,
        x.ScheduleType, x.IntervalDays, x.Weekday, x.ArchivedOn != null)).ToListAsync());
tasks.MapPost("/", async (TaskRequest request, AppDbContext db) =>
{
    var error = await ValidateTask(request, db);
    if (error is not null) return Results.BadRequest(new { error });
    var task = new TaskDefinition
    {
        ProfileId = request.ProfileId, CategoryId = request.CategoryId, Title = request.Title.Trim(),
        TargetSeconds = request.TargetSeconds, StartDate = request.StartDate, ScheduleType = request.ScheduleType,
        IntervalDays = request.ScheduleType == ScheduleType.IntervalDays ? request.IntervalDays : null,
        Weekday = request.ScheduleType == ScheduleType.Weekly ? request.Weekday : null
    };
    db.Tasks.Add(task);
    await db.SaveChangesAsync();
    return Results.Created($"/api/tasks/{task.Id}", new { task.Id });
});
tasks.MapPut("/{id:guid}", async (Guid id, TaskRequest request, AppDbContext db, OccurrenceService service) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();
    if (task.ProfileId != request.ProfileId) return Results.BadRequest(new { error = "A task cannot move between profiles." });
    var error = await ValidateTask(request, db);
    if (error is not null) return Results.BadRequest(new { error });
    var today = DateOnly.FromDateTime(DateTime.Now);
    await service.MaterializeThroughTodayAsync(task, today);
    var unstartedFuture = await db.Occurrences
        .Where(x => x.TaskDefinitionId == task.Id && x.ScheduledDate > today && !x.Sessions.Any())
        .ToListAsync();
    db.Occurrences.RemoveRange(unstartedFuture);
    task.CategoryId = request.CategoryId;
    task.Title = request.Title.Trim();
    task.TargetSeconds = request.TargetSeconds;
    task.StartDate = request.StartDate > today ? request.StartDate : today.AddDays(1);
    task.ScheduleType = request.ScheduleType;
    task.IntervalDays = request.ScheduleType == ScheduleType.IntervalDays ? request.IntervalDays : null;
    task.Weekday = request.ScheduleType == ScheduleType.Weekly ? request.Weekday : null;
    await db.SaveChangesAsync();
    return Results.NoContent();
});
tasks.MapPost("/{id:guid}/archive", async (Guid id, AppDbContext db, OccurrenceService service) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();
    var today = DateOnly.FromDateTime(DateTime.Now);
    await service.MaterializeThroughTodayAsync(task, today);
    var unstartedFuture = await db.Occurrences
        .Where(x => x.TaskDefinitionId == task.Id && x.ScheduledDate > today && !x.Sessions.Any())
        .ToListAsync();
    db.Occurrences.RemoveRange(unstartedFuture);
    task.ArchivedOn = today.AddDays(1);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/today", async (Guid profileId, DateOnly? date, AppDbContext db, OccurrenceService service) =>
{
    var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
    await service.EnsureAsync(profileId, targetDate, targetDate);
    var now = DateTime.UtcNow;
    var items = await db.Occurrences.Include(x => x.Sessions).Include(x => x.TaskDefinition)!.ThenInclude(x => x!.Category)
        .Where(x => x.TaskDefinition!.ProfileId == profileId && x.ScheduledDate == targetDate).ToListAsync();
    return items.OrderBy(x => x.TaskDefinition!.Title).Select(x => ToOccurrenceDto(x, now));
});

var timer = app.MapGroup("/api/timer");
timer.MapGet("/active", async (Guid profileId, AppDbContext db) =>
{
    var session = await db.Sessions.Include(x => x.TaskOccurrence)!.ThenInclude(x => x!.TaskDefinition)
        .FirstOrDefaultAsync(x => x.EndedAtUtc == null && x.TaskOccurrence!.TaskDefinition!.ProfileId == profileId);
    return session is null ? Results.NoContent() : Results.Ok(new ActiveTimerDto(session.Id, session.TaskOccurrenceId, session.StartedAtUtc));
});
timer.MapPost("/start", async (StartTimerRequest request, AppDbContext db) =>
{
    var occurrence = await db.Occurrences.Include(x => x.TaskDefinition).FirstOrDefaultAsync(x => x.Id == request.OccurrenceId);
    if (occurrence?.TaskDefinition?.ProfileId != request.ProfileId) return Results.BadRequest(new { error = "Invalid occurrence." });
    await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var now = DateTime.UtcNow;
    var open = await db.Sessions.Include(x => x.TaskOccurrence)!.ThenInclude(x => x!.TaskDefinition)
        .Where(x => x.EndedAtUtc == null && x.TaskOccurrence!.TaskDefinition!.ProfileId == request.ProfileId).ToListAsync();
    foreach (var old in open) old.EndedAtUtc = now;
    db.Sessions.Add(new TimeSession { TaskOccurrenceId = request.OccurrenceId, StartedAtUtc = now });
    await db.SaveChangesAsync();
    await UpdateCompletions(request.ProfileId, db, now);
    await transaction.CommitAsync();
    return Results.Ok(new { startedAtUtc = now });
});
timer.MapPost("/stop", async (Guid profileId, AppDbContext db) =>
{
    var now = DateTime.UtcNow;
    var open = await db.Sessions.Include(x => x.TaskOccurrence)!.ThenInclude(x => x!.TaskDefinition)
        .Where(x => x.EndedAtUtc == null && x.TaskOccurrence!.TaskDefinition!.ProfileId == profileId).ToListAsync();
    foreach (var session in open) session.EndedAtUtc = now;
    await db.SaveChangesAsync();
    await UpdateCompletions(profileId, db, now);
    return Results.NoContent();
});

app.MapGet("/api/reports", async (Guid profileId, DateOnly from, DateOnly to, Guid? categoryId, AppDbContext db, OccurrenceService service) =>
{
    if (to < from || to.DayNumber - from.DayNumber > 370) return Results.BadRequest(new { error = "The report range must be between 1 and 371 days." });
    await service.EnsureAsync(profileId, from, to);
    var now = DateTime.UtcNow;
    var query = db.Occurrences.Include(x => x.Sessions).Include(x => x.TaskDefinition)!.ThenInclude(x => x!.Category)
        .Where(x => x.TaskDefinition!.ProfileId == profileId && x.ScheduledDate >= from && x.ScheduledDate <= to);
    if (categoryId is not null) query = query.Where(x => x.TaskDefinition!.CategoryId == categoryId);
    var occurrences = await query.ToListAsync();
    var rows = occurrences.Select(x => new { Occurrence = x, Actual = OccurrenceService.ElapsedSeconds(x, now) }).ToList();
    var categoryRows = rows.GroupBy(x => new { x.Occurrence.TaskDefinition!.CategoryId, x.Occurrence.TaskDefinition.Category!.Name })
        .Select(g => new CategorySummaryDto(g.Key.CategoryId, g.Key.Name, g.Sum(x => (long)x.Occurrence.PlannedSeconds),
            g.Sum(x => x.Actual), g.Count(), g.Count(x => x.Actual >= x.Occurrence.PlannedSeconds))).OrderByDescending(x => x.ActualSeconds).ToList();
    var routineRows = rows.GroupBy(x => new
        {
            RoutineId = x.Occurrence.TaskDefinitionId,
            x.Occurrence.TaskDefinition!.Title,
            x.Occurrence.TaskDefinition.CategoryId,
            CategoryName = x.Occurrence.TaskDefinition.Category!.Name
        })
        .Select(g => new RoutineSummaryDto(g.Key.RoutineId, g.Key.Title, g.Key.CategoryId, g.Key.CategoryName,
            g.Sum(x => (long)x.Occurrence.PlannedSeconds), g.Sum(x => x.Actual), g.Count(),
            g.Count(x => x.Actual >= x.Occurrence.PlannedSeconds)))
        .OrderByDescending(x => x.ActualSeconds).ThenBy(x => x.Title).ToList();
    var days = rows.GroupBy(x => x.Occurrence.ScheduledDate).Select(g => new DaySummaryDto(g.Key, g.Sum(x => x.Actual),
        g.Where(x => x.Actual >= x.Occurrence.PlannedSeconds).Select(x => new DayTaskDto(x.Occurrence.TaskDefinition!.Title,
            x.Occurrence.TaskDefinition.Category!.Name, x.Actual, x.Actual >= x.Occurrence.PlannedSeconds)).ToList())).OrderBy(x => x.Date).ToList();
    var recent = rows.Where(x => x.Actual >= x.Occurrence.PlannedSeconds)
        .Select(x => new RecentCompletionDto(x.Occurrence.Id, x.Occurrence.TaskDefinition!.Title,
            x.Occurrence.TaskDefinition.Category!.Name, x.Occurrence.ScheduledDate, x.Actual,
            x.Occurrence.CompletedAtUtc ?? x.Occurrence.Sessions.Max(s => s.EndedAtUtc ?? now)))
        .OrderByDescending(x => x.CompletedAtUtc).Take(5).ToList();
    return Results.Ok(new ReportDto(from, to, rows.Sum(x => (long)x.Occurrence.PlannedSeconds), rows.Sum(x => x.Actual),
        rows.Count, rows.Count(x => x.Actual >= x.Occurrence.PlannedSeconds), categoryRows, routineRows, days, recent));
});

app.Run();

static async Task<string?> ValidateTask(TaskRequest request, AppDbContext db)
{
    if (request.Title.Trim().Length is < 1 or > 140) return "Title must be between 1 and 140 characters.";
    if (request.TargetSeconds is < 60 or > 86400) return "Target duration must be between one minute and 24 hours.";
    var category = await db.Categories.FirstOrDefaultAsync(x => x.Id == request.CategoryId && x.ProfileId == request.ProfileId);
    if (category is null || category.IsArchived) return "Choose an active category from this profile.";
    if (request.ScheduleType == ScheduleType.IntervalDays && request.IntervalDays is < 2 or > 365) return "The interval must be between 2 and 365 days.";
    if (request.ScheduleType == ScheduleType.Weekly && request.Weekday is null) return "Choose a weekday.";
    return null;
}

static OccurrenceDto ToOccurrenceDto(TaskOccurrence x, DateTime now)
{
    var elapsed = OccurrenceService.ElapsedSeconds(x, now);
    var running = x.Sessions.FirstOrDefault(s => s.EndedAtUtc == null);
    return new OccurrenceDto(x.Id, x.TaskDefinitionId, x.TaskDefinition!.Title, x.TaskDefinition.CategoryId,
        x.TaskDefinition.Category!.Name, x.ScheduledDate, x.PlannedSeconds, elapsed,
        elapsed >= x.PlannedSeconds, running is not null, running?.StartedAtUtc);
}

static async Task UpdateCompletions(Guid profileId, AppDbContext db, DateTime now)
{
    var candidates = await db.Occurrences.Include(x => x.Sessions).Include(x => x.TaskDefinition)
        .Where(x => x.CompletedAtUtc == null && x.TaskDefinition!.ProfileId == profileId).ToListAsync();
    foreach (var occurrence in candidates)
        if (OccurrenceService.ElapsedSeconds(occurrence, now) >= occurrence.PlannedSeconds)
            occurrence.CompletedAtUtc = now;
    await db.SaveChangesAsync();
}

public partial class Program { }
