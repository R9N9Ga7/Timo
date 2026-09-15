using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Timo.Api;
using Xunit;

namespace Timo.Api.Tests;

public sealed class DataBackupServiceTests
{
    [Fact]
    public async Task Export_and_import_round_trip_all_data_and_freeze_active_sessions()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"timo-backup-source-{Guid.NewGuid():N}.db");
        var targetPath = Path.Combine(Path.GetTempPath(), $"timo-backup-target-{Guid.NewGuid():N}.db");
        var backupDirectory = Path.Combine(Path.GetTempPath(), $"timo-backups-{Guid.NewGuid():N}");

        try
        {
            var profile = new Profile { Name = "Test" };
            var category = new Category { ProfileId = profile.Id, Name = "Study" };
            var task = new TaskDefinition
            {
                ProfileId = profile.Id, CategoryId = category.Id, Title = "Read", TargetSeconds = 1800,
                StartDate = new DateOnly(2026, 9, 15), ScheduleType = ScheduleType.Daily
            };
            var occurrence = new TaskOccurrence
            {
                TaskDefinitionId = task.Id, ScheduledDate = new DateOnly(2026, 9, 15), PlannedSeconds = 1800
            };
            var session = new TimeSession
            {
                TaskOccurrenceId = occurrence.Id, StartedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };

            await using (var source = CreateDb(sourcePath))
            {
                await source.Database.EnsureCreatedAsync();
                source.AddRange(profile, category, task, occurrence, session);
                await source.SaveChangesAsync();
            }

            DataBackupDto backup;
            await using (var source = CreateDb(sourcePath))
                backup = await new DataBackupService(source, new DataBackupOptions(backupDirectory)).ExportAsync();

            Assert.Single(backup.Profiles);
            Assert.Single(backup.Categories);
            Assert.Single(backup.Tasks);
            Assert.Single(backup.Occurrences);
            Assert.Single(backup.Sessions);
            Assert.NotNull(backup.Sessions[0].EndedAtUtc);
            Assert.True(backup.Sessions[0].EndedAtUtc >= backup.Sessions[0].StartedAtUtc);

            await using (var target = CreateDb(targetPath))
            {
                await target.Database.EnsureCreatedAsync();
                target.Profiles.Add(new Profile { Name = "Replace me" });
                await target.SaveChangesAsync();
                await new DataBackupService(target, new DataBackupOptions(backupDirectory)).ImportAsync(backup);
            }

            var safetyBackup = Assert.Single(Directory.GetFiles(backupDirectory, "timo-pre-import-*.json"));
            Assert.Contains("Replace me", await File.ReadAllTextAsync(safetyBackup));

            await using (var verification = CreateDb(targetPath))
            {
                Assert.Equal("Test", (await verification.Profiles.SingleAsync()).Name);
                Assert.Equal("Study", (await verification.Categories.SingleAsync()).Name);
                Assert.Equal("Read", (await verification.Tasks.SingleAsync()).Title);
                Assert.Equal(1800, (await verification.Occurrences.SingleAsync()).PlannedSeconds);
                Assert.NotNull((await verification.Sessions.SingleAsync()).EndedAtUtc);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteDatabase(sourcePath);
            DeleteDatabase(targetPath);
            if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true);
        }
    }

    [Fact]
    public void Validation_rejects_broken_relationships()
    {
        var backup = new DataBackupDto(DataBackupService.CurrentSchemaVersion, DateTime.UtcNow,
            [], [new CategoryBackupDto(Guid.NewGuid(), Guid.NewGuid(), "Orphan", false, DateTime.UtcNow)],
            [], [], []);

        Assert.Equal("The backup contains an invalid category.", DataBackupService.Validate(backup));
    }

    private static AppDbContext CreateDb(string path) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={path}").Options);

    private static void DeleteDatabase(string path)
    {
        File.Delete(path);
        File.Delete($"{path}-shm");
        File.Delete($"{path}-wal");
    }
}
