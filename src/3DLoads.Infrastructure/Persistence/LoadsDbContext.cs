using Microsoft.EntityFrameworkCore;
using ThreeDLoads.Domain.Entities;

namespace ThreeDLoads.Infrastructure.Persistence;

// EF Core database context. All entity configurations are done here via Fluent API
// (snake_case column names are applied globally by UseSnakeCaseNamingConvention).
public sealed class LoadsDbContext(DbContextOptions<LoadsDbContext> options) : DbContext(options)
{
    public DbSet<Van> Vans => Set<Van>();
    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<PackageItem> PackageItems => Set<PackageItem>();
    public DbSet<SolveJob> SolveJobs => Set<SolveJob>();
    public DbSet<Solution> Solutions => Set<Solution>();
    public DbSet<Placement> Placements => Set<Placement>();
    public DbSet<UnpackedItem> UnpackedItems => Set<UnpackedItem>();
    public DbSet<PhaseMetric> PhaseMetrics => Set<PhaseMetric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Van>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.HasOne(x => x.Van)
                .WithMany()
                .HasForeignKey(x => x.VanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Packages)
                .WithOne(x => x.Scenario)
                .HasForeignKey(x => x.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PackageItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalReference).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<SolveJob>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestedPhases).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.HasOne(x => x.Scenario)
                .WithMany()
                .HasForeignKey(x => x.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Solution)
                .WithMany()
                .HasForeignKey(x => x.SolutionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Solution>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ValidationErrorsJson)
                .HasConversion(
                    v => string.IsNullOrWhiteSpace(v) ? "[]" : v,
                    v => string.IsNullOrWhiteSpace(v) ? "[]" : v)
                .HasColumnType("text");
            entity.HasOne(x => x.Scenario)
                .WithMany()
                .HasForeignKey(x => x.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Placements)
                .WithOne(x => x.Solution)
                .HasForeignKey(x => x.SolutionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.UnpackedItems)
                .WithOne(x => x.Solution)
                .HasForeignKey(x => x.SolutionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.PhaseMetrics)
                .WithOne(x => x.Solution)
                .HasForeignKey(x => x.SolutionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Placement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.PackageItem)
                .WithMany()
                .HasForeignKey(x => x.PackageItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UnpackedItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.HasOne(x => x.PackageItem)
                .WithMany()
                .HasForeignKey(x => x.PackageItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PhaseMetric>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PhaseName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.TelemetryJson)
                .HasColumnType("text")
                .HasDefaultValue("{}");
        });
    }
}