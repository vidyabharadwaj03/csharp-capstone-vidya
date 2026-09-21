using Microsoft.EntityFrameworkCore;
using ReservationService.Models;

namespace ReservationService.Data;

public class ReservationServiceContext : DbContext
{
    public ReservationServiceContext(DbContextOptions<ReservationServiceContext> options) : base(options)
    {
    }

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<Waitlist> WaitlistEntries => Set<Waitlist>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(r => r.ReservationId);
            entity.Property(r => r.Status).HasConversion<string>();
            entity.Property(r => r.Condition).HasConversion<string>();
            entity.Property(r => r.LateFee).HasPrecision(10, 2);
            entity.HasIndex(r => r.UserId);
            entity.HasIndex(r => r.BookId);
        });

        modelBuilder.Entity<Waitlist>(entity =>
        {
            entity.HasKey(w => w.WaitlistId);
            entity.Property(w => w.Status).HasConversion<string>();
            entity.HasIndex(w => w.BookId);
            entity.HasIndex(w => w.UserId);
        });

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Reservation>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Waitlist>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
