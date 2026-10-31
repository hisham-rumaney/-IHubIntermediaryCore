using IHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IHub.Data.Persistence;

public sealed class IHubDbContext : DbContext
{
    public IHubDbContext(DbContextOptions<IHubDbContext> options) : base(options)
    {
    }

    public DbSet<Intermediary> Intermediaries => Set<Intermediary>();
    public DbSet<Activity> Activities => Set<Activity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Intermediary>(entity =>
        {
            entity.HasKey(x => x.IntermediaryId);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceSystem).IsRequired().HasMaxLength(50);
            entity.Property(x => x.ExternalActivityId).IsRequired().HasMaxLength(200);
            entity.Property(x => x.IntermediaryId).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            entity.Property(x => x.OccurredAt).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)").IsRequired();
            entity.Property(x => x.IngestedAt).IsRequired();
            entity.HasAlternateKey(x => new { x.SourceSystem, x.ExternalActivityId })
                .HasName("UQ_Activities_SourceSystem_ExternalActivityId");
            entity.HasIndex(x => new { x.IntermediaryId, x.OccurredAt })
                .HasDatabaseName("IX_Activities_IntermediaryId_OccurredAt");
            entity.HasOne<Intermediary>()
                .WithMany()
                .HasForeignKey(x => x.IntermediaryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
