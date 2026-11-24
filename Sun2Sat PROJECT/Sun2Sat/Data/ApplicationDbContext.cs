using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sun2Sat.Models;

namespace Sun2Sat.Data;

// Replaces the ApplicationDbContext the Blazor template generates.
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventTemplate> EventTemplates => Set<EventTemplate>();
    public DbSet<EventParticipation> EventParticipations => Set<EventParticipation>();
    public DbSet<EventInvitation> EventInvitations => Set<EventInvitation>();
    public DbSet<UserConnection> Connections => Set<UserConnection>();
    public DbSet<UnavailableBlock> UnavailableBlocks => Set<UnavailableBlock>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Store and read every DateTime as UTC (see UtcDateTimeConverter).
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Must run first: it sets up all the Identity tables (users, roles, logins...).
        base.OnModelCreating(builder);

        builder.Entity<Event>(e =>
        {
            e.HasOne(x => x.Creator)
                .WithMany(u => u.CreatedEvents)
                .HasForeignKey(x => x.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Template)
                .WithMany()
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            // Calendar views query by time range; search queries by name.
            e.HasIndex(x => x.StartUtc);
            e.HasIndex(x => x.Name);
        });

        builder.Entity<EventParticipation>(p =>
        {
            // Composite key: a user can have only one status per event.
            p.HasKey(x => new { x.EventId, x.UserId });

            p.HasOne(x => x.Event)
                .WithMany(ev => ev.Participants)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            p.HasOne(x => x.User)
                .WithMany(u => u.Participations)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventInvitation>(i =>
        {
            i.HasOne(x => x.Event)
                .WithMany(ev => ev.Invitations)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            i.HasOne(x => x.Sender)
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Cascade);

            i.HasOne(x => x.Recipient)
                .WithMany()
                .HasForeignKey(x => x.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);

            // A person can be invited to a given event only once.
            i.HasIndex(x => new { x.EventId, x.RecipientId }).IsUnique();
        });

        builder.Entity<UserConnection>(c =>
        {
            c.HasOne(x => x.Requester)
                .WithMany(u => u.SentConnectionRequests)
                .HasForeignKey(x => x.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);

            c.HasOne(x => x.Addressee)
                .WithMany(u => u.ReceivedConnectionRequests)
                .HasForeignKey(x => x.AddresseeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Blocks duplicate A->B requests. The service layer also checks B->A,
            // since the database can't express "either direction" as one index.
            c.HasIndex(x => new { x.RequesterId, x.AddresseeId }).IsUnique();

            // Fast lookup of "pending requests waiting on me" for the feed.
            c.HasIndex(x => new { x.AddresseeId, x.Status });
        });

        builder.Entity<UnavailableBlock>(b =>
        {
            b.HasOne(x => x.User)
                .WithMany(u => u.UnavailableBlocks)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.UserId, x.StartUtc });
        });

        builder.Entity<Notification>(n =>
        {
            n.HasOne(x => x.Recipient)
                .WithMany(u => u.Notifications)
                .HasForeignKey(x => x.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);

            n.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            n.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.SetNull);

            // "My unread notifications, newest first."
            n.HasIndex(x => new { x.RecipientId, x.IsRead, x.CreatedAtUtc });
        });

        // General events offered in the "Add event" dialog.
        builder.Entity<EventTemplate>().HasData(
            new EventTemplate { Id = 1, Name = "Work",     DefaultDurationMinutes = 480 },
            new EventTemplate { Id = 2, Name = "Gym",      DefaultDurationMinutes = 60 },
            new EventTemplate { Id = 3, Name = "Class",    DefaultDurationMinutes = 75 },
            new EventTemplate { Id = 4, Name = "Study",    DefaultDurationMinutes = 90 },
            new EventTemplate { Id = 5, Name = "Practice", DefaultDurationMinutes = 120 },
            new EventTemplate { Id = 6, Name = "Meal",     DefaultDurationMinutes = 60 }
        );
    }
}
