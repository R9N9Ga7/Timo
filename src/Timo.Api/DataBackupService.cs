using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Timo.Api;

public sealed record DataBackupOptions(string BackupDirectory);

public sealed class DataBackupService(AppDbContext db, DataBackupOptions options)
{
    public const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions BackupJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<DataBackupDto> ExportAsync(CancellationToken ct = default)
    {
        var exportedAtUtc = DateTime.UtcNow;
        var profiles = await db.Profiles.AsNoTracking().OrderBy(x => x.CreatedAtUtc)
            .Select(x => new ProfileBackupDto(x.Id, x.Name, x.CreatedAtUtc)).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().OrderBy(x => x.CreatedAtUtc)
            .Select(x => new CategoryBackupDto(x.Id, x.ProfileId, x.Name, x.IsArchived, x.CreatedAtUtc)).ToListAsync(ct);
        var tasks = await db.Tasks.AsNoTracking().OrderBy(x => x.CreatedAtUtc)
            .Select(x => new TaskBackupDto(x.Id, x.ProfileId, x.CategoryId, x.Title, x.TargetSeconds,
                x.StartDate, x.ScheduleType, x.IntervalDays, x.Weekday, x.ArchivedOn, x.CreatedAtUtc)).ToListAsync(ct);
        var occurrences = await db.Occurrences.AsNoTracking().OrderBy(x => x.ScheduledDate)
            .Select(x => new OccurrenceBackupDto(x.Id, x.TaskDefinitionId, x.ScheduledDate,
                x.PlannedSeconds, x.CompletedAtUtc)).ToListAsync(ct);
        var sessions = await db.Sessions.AsNoTracking().OrderBy(x => x.StartedAtUtc)
            .Select(x => new SessionBackupDto(x.Id, x.TaskOccurrenceId, x.StartedAtUtc,
                x.EndedAtUtc ?? exportedAtUtc)).ToListAsync(ct);

        return new DataBackupDto(CurrentSchemaVersion, exportedAtUtc, profiles, categories, tasks, occurrences, sessions);
    }

    public async Task ImportAsync(DataBackupDto backup, CancellationToken ct = default)
    {
        var error = Validate(backup);
        if (error is not null) throw new InvalidDataException(error);

        await SaveCurrentBackupAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Sessions.ExecuteDeleteAsync(ct);
        await db.Occurrences.ExecuteDeleteAsync(ct);
        await db.Tasks.ExecuteDeleteAsync(ct);
        await db.Categories.ExecuteDeleteAsync(ct);
        await db.Profiles.ExecuteDeleteAsync(ct);

        db.Profiles.AddRange(backup.Profiles.Select(x => new Profile
            { Id = x.Id, Name = x.Name, CreatedAtUtc = x.CreatedAtUtc }));
        db.Categories.AddRange(backup.Categories.Select(x => new Category
            { Id = x.Id, ProfileId = x.ProfileId, Name = x.Name, IsArchived = x.IsArchived, CreatedAtUtc = x.CreatedAtUtc }));
        db.Tasks.AddRange(backup.Tasks.Select(x => new TaskDefinition
        {
            Id = x.Id, ProfileId = x.ProfileId, CategoryId = x.CategoryId, Title = x.Title,
            TargetSeconds = x.TargetSeconds, StartDate = x.StartDate, ScheduleType = x.ScheduleType,
            IntervalDays = x.IntervalDays, Weekday = x.Weekday, ArchivedOn = x.ArchivedOn, CreatedAtUtc = x.CreatedAtUtc
        }));
        db.Occurrences.AddRange(backup.Occurrences.Select(x => new TaskOccurrence
        {
            Id = x.Id, TaskDefinitionId = x.TaskDefinitionId, ScheduledDate = x.ScheduledDate,
            PlannedSeconds = x.PlannedSeconds, CompletedAtUtc = x.CompletedAtUtc
        }));
        db.Sessions.AddRange(backup.Sessions.Select(x => new TimeSession
        {
            Id = x.Id, TaskOccurrenceId = x.TaskOccurrenceId, StartedAtUtc = x.StartedAtUtc, EndedAtUtc = x.EndedAtUtc
        }));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task SaveCurrentBackupAsync(CancellationToken ct)
    {
        var current = await ExportAsync(ct);
        Directory.CreateDirectory(options.BackupDirectory);
        var name = $"timo-pre-import-{current.ExportedAtUtc:yyyyMMdd-HHmmss-fff}.json";
        var destination = Path.Combine(options.BackupDirectory, name);
        var temporary = Path.Combine(options.BackupDirectory, $".{name}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(current, BackupJsonOptions), ct);
            File.Move(temporary, destination);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public static string? Validate(DataBackupDto? backup)
    {
        if (backup is null) return "Choose a valid Timo backup file.";
        if (backup.SchemaVersion != CurrentSchemaVersion) return "This backup version is not supported.";
        if (backup.Profiles is null || backup.Categories is null || backup.Tasks is null ||
            backup.Occurrences is null || backup.Sessions is null) return "The backup is incomplete.";
        if (HasDuplicateIds(backup.Profiles.Select(x => x.Id)) || HasDuplicateIds(backup.Categories.Select(x => x.Id)) ||
            HasDuplicateIds(backup.Tasks.Select(x => x.Id)) || HasDuplicateIds(backup.Occurrences.Select(x => x.Id)) ||
            HasDuplicateIds(backup.Sessions.Select(x => x.Id))) return "The backup contains duplicate IDs.";

        var profileIds = backup.Profiles.Select(x => x.Id).ToHashSet();
        if (backup.Profiles.Any(x => x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 80))
            return "The backup contains an invalid profile.";
        if (backup.Categories.Any(x => x.Id == Guid.Empty || !profileIds.Contains(x.ProfileId) ||
                string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 80))
            return "The backup contains an invalid category.";
        if (backup.Categories.GroupBy(x => new { x.ProfileId, Name = x.Name.Trim().ToUpperInvariant() }).Any(x => x.Count() > 1))
            return "The backup contains duplicate category names.";

        var categories = backup.Categories.ToDictionary(x => x.Id);
        if (backup.Tasks.Any(x => x.Id == Guid.Empty || !profileIds.Contains(x.ProfileId) ||
                !categories.TryGetValue(x.CategoryId, out var category) || category.ProfileId != x.ProfileId ||
                string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 140 || x.TargetSeconds is < 60 or > 86400 ||
                !Enum.IsDefined(x.ScheduleType) ||
                (x.ScheduleType == ScheduleType.IntervalDays && x.IntervalDays is null or < 2 or > 365) ||
                (x.ScheduleType == ScheduleType.Weekly && (x.Weekday is null || !Enum.IsDefined(x.Weekday.Value)))))
            return "The backup contains an invalid routine.";

        var taskIds = backup.Tasks.Select(x => x.Id).ToHashSet();
        if (backup.Occurrences.Any(x => x.Id == Guid.Empty || !taskIds.Contains(x.TaskDefinitionId) ||
                x.PlannedSeconds is < 60 or > 86400) ||
            backup.Occurrences.GroupBy(x => new { x.TaskDefinitionId, x.ScheduledDate }).Any(x => x.Count() > 1))
            return "The backup contains an invalid routine occurrence.";

        var occurrenceIds = backup.Occurrences.Select(x => x.Id).ToHashSet();
        if (backup.Sessions.Any(x => x.Id == Guid.Empty || !occurrenceIds.Contains(x.TaskOccurrenceId) ||
                x.EndedAtUtc is null || x.EndedAtUtc < x.StartedAtUtc))
            return "The backup contains an invalid timer session.";

        return null;
    }

    private static bool HasDuplicateIds(IEnumerable<Guid> ids)
    {
        var values = ids.ToList();
        return values.Count != values.Distinct().Count();
    }
}
