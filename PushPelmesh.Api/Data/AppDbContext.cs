using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Models;

namespace PushPelmesh.Api.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<AccessKey> AccessKeys => Set<AccessKey>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<XoxiSave> XoxiSaves => Set<XoxiSave>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarEventNotification> CalendarEventNotifications => Set<CalendarEventNotification>();
    public DbSet<CalendarEventPushDelivery> CalendarEventPushDeliveries => Set<CalendarEventPushDelivery>();
    public DbSet<PushNotificationSubscription> PushNotificationSubscriptions => Set<PushNotificationSubscription>();
    public DbSet<UserModuleNotification> UserModuleNotifications => Set<UserModuleNotification>();
    public DbSet<RewardRecord> RewardRecords => Set<RewardRecord>();
    public DbSet<CinemaMovieRating> CinemaMovieRatings => Set<CinemaMovieRating>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<XoxiSave>(entity =>
        {
            entity.ToTable("xoxi_saves");

            entity.HasKey(x => x.UserId);

            entity.Property(x => x.UserId)
                .HasColumnName("user_id");

            entity.Property(x => x.SaveData)
                .HasColumnName("save_data")
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.HasOne(x => x.User)
                .WithOne()
                .HasForeignKey<XoxiSave>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CalendarEventNotification>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
                {
                    x.CalendarEventId,
                    x.SentForDate
                })
                .IsUnique();

            entity.HasOne(x => x.CalendarEvent)
                .WithMany()
                .HasForeignKey(x => x.CalendarEventId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<CalendarEventPushDelivery>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
                {
                    x.CalendarEventId,
                    x.PushNotificationSubscriptionId,
                    x.SentForDate
                })
                .IsUnique();

            entity.HasOne(x => x.CalendarEvent)
                .WithMany()
                .HasForeignKey(x => x.CalendarEventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.PushNotificationSubscription)
                .WithMany()
                .HasForeignKey(x => x.PushNotificationSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserModuleNotification>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
                {
                    x.UserId,
                    x.ModuleKey,
                    x.SourceKey
                })
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.UserId,
                x.ReadAt
            });

            entity.Property(x => x.ModuleKey)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(x => x.SourceKey)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasMaxLength(256);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RewardRecord>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Kind);

            entity.Property(x => x.FullName)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(x => x.EventType)
                .HasMaxLength(128);

            entity.Property(x => x.EventName)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(x => x.Place)
                .HasMaxLength(128);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CinemaMovieRating>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(x => x.Rating)
                .HasPrecision(4, 2);

            entity.Property(x => x.Url)
                .HasMaxLength(1024);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}


