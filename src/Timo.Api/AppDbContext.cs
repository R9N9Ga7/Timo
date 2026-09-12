using Microsoft.EntityFrameworkCore;

namespace Timo.Api;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<TaskDefinition> Tasks => Set<TaskDefinition>();
    public DbSet<TaskOccurrence> Occurrences => Set<TaskOccurrence>();
    public DbSet<TimeSession> Sessions => Set<TimeSession>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Category>().HasIndex(x => new { x.ProfileId, x.Name }).IsUnique();
        model.Entity<TaskDefinition>().Property(x => x.ScheduleType).HasConversion<string>();
        model.Entity<TaskOccurrence>().HasIndex(x => new { x.TaskDefinitionId, x.ScheduledDate }).IsUnique();
        model.Entity<TimeSession>().HasIndex(x => x.EndedAtUtc);
        model.Entity<Profile>().HasMany<Category>().WithOne(x => x.Profile!).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<Profile>().HasMany<TaskDefinition>().WithOne().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<Category>().HasMany<TaskDefinition>().WithOne(x => x.Category!).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<TaskDefinition>().HasMany<TaskOccurrence>().WithOne(x => x.TaskDefinition!).HasForeignKey(x => x.TaskDefinitionId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<TaskOccurrence>().HasMany(x => x.Sessions).WithOne(x => x.TaskOccurrence!).HasForeignKey(x => x.TaskOccurrenceId).OnDelete(DeleteBehavior.Cascade);
    }
}
