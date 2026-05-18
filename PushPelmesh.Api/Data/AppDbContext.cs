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
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

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
    }
}
