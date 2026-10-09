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
    public DbSet<VotePoll> VotePolls => Set<VotePoll>();
    public DbSet<VoteOption> VoteOptions => Set<VoteOption>();
    public DbSet<VoteBallot> VoteBallots => Set<VoteBallot>();
    public DbSet<ZongRoom> ZongRooms => Set<ZongRoom>();
    public DbSet<ZongRoomPlayer> ZongRoomPlayers => Set<ZongRoomPlayer>();
    public DbSet<DurakRoom> DurakRooms => Set<DurakRoom>();
    public DbSet<DurakRoomPlayer> DurakRoomPlayers => Set<DurakRoomPlayer>();
    public DbSet<RememberedLogin> RememberedLogins => Set<RememberedLogin>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatNotificationSetting> ChatNotificationSettings => Set<ChatNotificationSetting>();

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

            entity.HasIndex(x => x.Date);

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

        modelBuilder.Entity<VotePoll>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.EndDate);
            entity.HasIndex(x => x.AudienceGroups);

            entity.Property(x => x.Title)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(2048);

            entity.Property(x => x.CreatedByUserName)
                .HasMaxLength(256)
                .IsRequired();

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VoteOption>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
                {
                    x.VotePollId,
                    x.SortOrder
                });

            entity.Property(x => x.Text)
                .HasMaxLength(256)
                .IsRequired();

            entity.HasOne(x => x.VotePoll)
                .WithMany(x => x.Options)
                .HasForeignKey(x => x.VotePollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VoteBallot>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
                {
                    x.VotePollId,
                    x.UserId,
                    x.VoteOptionId
                })
                .IsUnique();

            entity.HasOne(x => x.VotePoll)
                .WithMany(x => x.Ballots)
                .HasForeignKey(x => x.VotePollId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.VoteOption)
                .WithMany(x => x.Ballots)
                .HasForeignKey(x => x.VoteOptionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<ZongRoom>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.UpdatedAt);

            entity.Property(x => x.Name)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .HasMaxLength(128);

            entity.Property(x => x.LastDice)
                .HasMaxLength(64);

            entity.Property(x => x.LastRollMessage)
                .HasMaxLength(512);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentTurnPlayer)
                .WithMany()
                .HasForeignKey(x => x.CurrentTurnPlayerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.WinnerPlayer)
                .WithMany()
                .HasForeignKey(x => x.WinnerPlayerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ZongRoomPlayer>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
                {
                    x.ZongRoomId,
                    x.UserId
                })
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.ZongRoomId,
                x.TurnOrder
            });

            entity.Property(x => x.DisplayName)
                .HasMaxLength(256)
                .IsRequired();

            entity.HasOne(x => x.ZongRoom)
                .WithMany(x => x.Players)
                .HasForeignKey(x => x.ZongRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DurakRoom>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.UpdatedAt);

            entity.Property(x => x.Name)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .HasMaxLength(128);

            entity.Property(x => x.TrumpSuit)
                .HasMaxLength(32);

            entity.Property(x => x.TrumpCardCode)
                .HasMaxLength(8);

            entity.Property(x => x.DeckJson)
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(x => x.TableJson)
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(x => x.DiscardJson)
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(x => x.LastMessage)
                .HasMaxLength(512);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AttackerPlayer)
                .WithMany()
                .HasForeignKey(x => x.AttackerPlayerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.DefenderPlayer)
                .WithMany()
                .HasForeignKey(x => x.DefenderPlayerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.WinnerPlayer)
                .WithMany()
                .HasForeignKey(x => x.WinnerPlayerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DurakRoomPlayer>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
                {
                    x.DurakRoomId,
                    x.UserId
                })
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.DurakRoomId,
                x.TurnOrder
            });

            entity.Property(x => x.DisplayName)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(x => x.HandJson)
                .HasColumnType("jsonb")
                .IsRequired();

            entity.HasOne(x => x.DurakRoom)
                .WithMany(x => x.Players)
                .HasForeignKey(x => x.DurakRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<RememberedLogin>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.DirectKey).IsUnique();
            entity.Property(x => x.DirectKey).HasMaxLength(64);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatParticipant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ChatRoomId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.ChatRoom).WithMany(x => x.Participants)
                .HasForeignKey(x => x.ChatRoomId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ChatRoomId, x.Id });
            entity.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.ChatRoom).WithMany().HasForeignKey(x => x.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ChatNotificationSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.ChatId }).IsUnique();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}






