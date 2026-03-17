using AgendaApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AgendaApi.Data;

public class AgendaDbContext : DbContext
{
    public AgendaDbContext(DbContextOptions<AgendaDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<MonthImage> MonthImages { get; set; }
    public DbSet<CalendarShare> CalendarShares { get; set; }
    public DbSet<CalendarShareInvite> CalendarShareInvites { get; set; }
    public DbSet<CalendarSubscription> CalendarSubscriptions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.StartDateTime).IsRequired();
            entity.Property(e => e.EndDateTime).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.UserId).IsRequired();

            // Primary lookup index — most queries filter by UserId
            entity.HasIndex(e => e.UserId);
            // Composite index for date-range queries (GetEventOccurrences, GetEventsByDateRange)
            entity.HasIndex(e => new { e.UserId, e.StartDateTime });

            // Indexes for recurring event lookups
            entity.HasIndex(e => e.RecurrenceId);
            entity.HasIndex(e => new { e.ParentEventId, e.RecurrenceId })
                .HasFilter("\"RecurrenceId\" IS NOT NULL");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Events)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.GoogleId).IsRequired().HasMaxLength(255);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
            entity.Property(u => u.Name).IsRequired().HasMaxLength(255);
            entity.Property(u => u.ProfilePictureUrl).HasMaxLength(500);
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.LastLoginAt).IsRequired();

            entity.HasIndex(u => u.GoogleId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.Property(rt => rt.Token).IsRequired().HasMaxLength(500);
            entity.Property(rt => rt.ExpiresAt).IsRequired();
            entity.Property(rt => rt.CreatedAt).IsRequired();
            entity.Property(rt => rt.IsRevoked).IsRequired();

            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.HasIndex(rt => rt.UserId);

            entity.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonthImage>(entity =>
        {
            entity.HasKey(mi => mi.Id);
            entity.Property(mi => mi.Month).IsRequired();
            entity.Property(mi => mi.FileName).IsRequired().HasMaxLength(500);
            entity.Property(mi => mi.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(mi => mi.UploadedAt).IsRequired();
            entity.Property(mi => mi.UserId).IsRequired();

            entity.HasIndex(mi => new { mi.UserId, mi.Month }).IsUnique();

            entity.HasOne(mi => mi.User)
                .WithMany()
                .HasForeignKey(mi => mi.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CalendarShareInvite>(entity =>
        {
            entity.HasKey(csi => csi.Id);
            entity.Property(csi => csi.RecipientEmail).IsRequired().HasMaxLength(255);
            entity.Property(csi => csi.Permission).IsRequired();
            entity.Property(csi => csi.Status).IsRequired();
            entity.Property(csi => csi.CreatedAt).IsRequired();

            entity.HasIndex(csi => csi.SenderUserId);
            entity.HasIndex(csi => csi.RecipientEmail);
            entity.HasIndex(csi => csi.RecipientUserId)
                .HasFilter("\"RecipientUserId\" IS NOT NULL");
            entity.HasIndex(csi => csi.Status);

            entity.HasOne(csi => csi.SenderUser)
                .WithMany()
                .HasForeignKey(csi => csi.SenderUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(csi => csi.RecipientUser)
                .WithMany()
                .HasForeignKey(csi => csi.RecipientUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CalendarShare>(entity =>
        {
            entity.HasKey(cs => cs.Id);
            entity.Property(cs => cs.Permission).IsRequired();
            entity.Property(cs => cs.CreatedAt).IsRequired();

            entity.HasIndex(cs => new { cs.OwnerUserId, cs.SharedWithUserId }).IsUnique();
            entity.HasIndex(cs => cs.OwnerUserId);
            entity.HasIndex(cs => cs.SharedWithUserId);

            entity.HasOne(cs => cs.OwnerUser)
                .WithMany()
                .HasForeignKey(cs => cs.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(cs => cs.SharedWithUser)
                .WithMany()
                .HasForeignKey(cs => cs.SharedWithUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure owner and shared user are different
            entity.ToTable(t => t.HasCheckConstraint("CK_CalendarShares_DifferentUsers", "\"OwnerUserId\" != \"SharedWithUserId\""));
        });

        modelBuilder.Entity<CalendarSubscription>(entity =>
        {
            entity.HasKey(cs => cs.Id);
            entity.Property(cs => cs.Name).IsRequired().HasMaxLength(255);
            entity.Property(cs => cs.ICalUrl).IsRequired().HasMaxLength(2000);
            entity.Property(cs => cs.Color).HasMaxLength(7);
            entity.Property(cs => cs.SyncIntervalMinutes).IsRequired();
            entity.Property(cs => cs.IsActive).IsRequired();
            entity.Property(cs => cs.CreatedAt).IsRequired();
            entity.Property(cs => cs.UpdatedAt).IsRequired();
            entity.Property(cs => cs.UserId).IsRequired();

            entity.HasIndex(cs => cs.UserId);

            entity.HasOne(cs => cs.User)
                .WithMany()
                .HasForeignKey(cs => cs.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
