using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Timo.Api;
using Xunit;

namespace Timo.Api.Tests;

public sealed class OccurrenceConcurrencyTests
{
    [Fact]
    public async Task Parallel_generation_creates_one_occurrence_per_task_and_date()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"timo-concurrency-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        try
        {
            Guid profileId;
            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                var profile = new Profile { Name = "Test" };
                var category = new Category { ProfileId = profile.Id, Name = "Japanese" };
                setup.AddRange(profile, category, new TaskDefinition
                {
                    ProfileId = profile.Id,
                    CategoryId = category.Id,
                    Title = "Listening",
                    TargetSeconds = 1800,
                    StartDate = new DateOnly(2026, 9, 12),
                    ScheduleType = ScheduleType.Daily
                });
                await setup.SaveChangesAsync();
                profileId = profile.Id;
            }

            await using var firstDb = new AppDbContext(options);
            await using var secondDb = new AppDbContext(options);
            var day = new DateOnly(2026, 9, 12);
            await Task.WhenAll(
                new OccurrenceService(firstDb).EnsureAsync(profileId, day, day),
                new OccurrenceService(secondDb).EnsureAsync(profileId, day, day));

            await using var verification = new AppDbContext(options);
            Assert.Equal(1, await verification.Occurrences.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
            File.Delete($"{databasePath}-shm");
            File.Delete($"{databasePath}-wal");
        }
    }
}
