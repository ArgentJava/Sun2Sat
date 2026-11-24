using Microsoft.EntityFrameworkCore;
using Sun2Sat.Data;
using Sun2Sat.Models;

namespace Sun2Sat.Services;

/// <summary>Creating, editing, cancelling and viewing events, plus the calendar query.
///
/// WHO CAN SEE AN EVENT (checked in one place, VisibleTo):
///   - its creator, always;
///   - anyone, if it's Public or Official;
///   - the creator's connections, if it's Connections-only.
/// Cancelled events are invisible to everyone. Because the connection check reads
/// the live Connections table, removing a connection hides their connections-only
/// events at once, even ones you had marked Going.</summary>
public class EventService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ConnectionService connections,
    NotificationService notifications)
{
    // ------------------------------------------------------------------ visibility

    private static IQueryable<Event> VisibleTo(ApplicationDbContext db, string userId, List<string> connectionIds) =>
        db.Events.Where(e =>
            !e.IsCancelled &&
            (e.CreatorId == userId ||
             e.Visibility == EventVisibility.Public ||
             e.Source == EventSource.Official ||
             (e.Visibility == EventVisibility.Connections &&
              e.CreatorId != null && connectionIds.Contains(e.CreatorId))));

    /// <summary>Events overlapping [fromUtc, toUtc). For repeating events this is a coarse
    /// filter (the series has started and hasn't ended); Recurrence does the exact check.</summary>
    private static IQueryable<Event> InRange(IQueryable<Event> q, DateTime fromUtc, DateTime toUtc) =>
        q.Where(e => e.StartUtc < toUtc &&
                     ((e.Recurrence == RecurrenceFrequency.None && e.EndUtc > fromUtc) ||
                      (e.Recurrence != RecurrenceFrequency.None &&
                       (e.RecurrenceUntilUtc == null || e.RecurrenceUntilUtc >= fromUtc))));

    // ------------------------------------------------------------------ calendar

    /// <summary>My calendar: events I created, events I'm Going to or Interested in, and
    /// (optionally) my connections' events, expanded into occurrences.</summary>
    public async Task<List<CalendarItem>> GetCalendarAsync(
        string userId, DateTime fromUtc, DateTime toUtc, bool includeConnections)
    {
        var connectionIds = await connections.GetConnectionIdsAsync(userId);
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = InRange(VisibleTo(db, userId, connectionIds), fromUtc, toUtc)
            .Where(e => e.CreatorId == userId ||
                        e.Participants.Any(p => p.UserId == userId) ||
                        (includeConnections && e.CreatorId != null && connectionIds.Contains(e.CreatorId)));

        return await ExpandAsync(query, userId, fromUtc, toUtc);
    }

    /// <summary>Another user's schedule as the viewer is allowed to see it: events they
    /// created plus events they're Going to.</summary>
    public async Task<List<CalendarItem>> GetUserScheduleAsync(
        string viewerId, string ownerId, DateTime fromUtc, DateTime toUtc)
    {
        var connectionIds = await connections.GetConnectionIdsAsync(viewerId);
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = InRange(VisibleTo(db, viewerId, connectionIds), fromUtc, toUtc)
            .Where(e => e.CreatorId == ownerId ||
                        e.Participants.Any(p => p.UserId == ownerId && p.Status == ParticipationStatus.Going));

        return await ExpandAsync(query, viewerId, fromUtc, toUtc);
    }

    private static async Task<List<CalendarItem>> ExpandAsync(
        IQueryable<Event> query, string viewerId, DateTime fromUtc, DateTime toUtc)
    {
        var rows = await query
            .Select(e => new
            {
                Event = e,
                HostId = e.CreatorId,
                HostUserName = e.Creator != null ? e.Creator.UserName : null,
                HostDisplayName = e.Creator != null ? e.Creator.DisplayName : null,
                MyStatus = e.Participants
                    .Where(p => p.UserId == viewerId)
                    .Select(p => (ParticipationStatus?)p.Status)
                    .FirstOrDefault()
            })
            .AsNoTracking()
            .ToListAsync();

        var items = new List<CalendarItem>();
        foreach (var r in rows)
        {
            var kind = r.HostId == viewerId ? CalendarItemKind.Mine
                : r.MyStatus == ParticipationStatus.Going ? CalendarItemKind.Going
                : r.MyStatus == ParticipationStatus.Interested ? CalendarItemKind.Interested
                : CalendarItemKind.Connection;

            var host = r.HostId is null ? null : new UserSummary(r.HostId, r.HostUserName ?? "", r.HostDisplayName);

            foreach (var (start, end) in Recurrence.Occurrences(r.Event, fromUtc, toUtc))
            {
                items.Add(new CalendarItem(r.Event.Id, r.Event.Name, r.Event.Location, start, end,
                    kind, host, r.Event.Recurrence != RecurrenceFrequency.None));
            }
        }

        return items.OrderBy(i => i.StartUtc).ThenBy(i => i.Name).ToList();
    }

    // ------------------------------------------------------------------ single event

    public async Task<EventDetails?> GetDetailsAsync(int eventId, string userId)
    {
        var connectionIds = await connections.GetConnectionIdsAsync(userId);
        await using var db = await dbFactory.CreateDbContextAsync();

        var e = await VisibleTo(db, userId, connectionIds)
            .Include(x => x.Creator)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == eventId);

        if (e is null) return null;

        var participants = await db.EventParticipations
            .Where(p => p.EventId == eventId)
            .Select(p => new
            {
                p.UserId,
                p.Status,
                User = new UserSummary(p.User.Id, p.User.UserName!, p.User.DisplayName)
            })
            .ToListAsync();

        var attendees = participants
            .Where(p => p.Status == ParticipationStatus.Going)
            .Select(p => p.User)
            .OrderBy(u => u.NameToShow, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var host = e.Creator is null ? null : new UserSummary(e.Creator.Id, e.Creator.UserName ?? "", e.Creator.DisplayName);

        return new EventDetails(
            e,
            host,
            IsHost: e.CreatorId == userId,
            MyStatus: participants.FirstOrDefault(p => p.UserId == userId)?.Status,
            Attendees: attendees,
            InterestedCount: participants.Count(p => p.Status == ParticipationStatus.Interested),
            RecurrenceText: Recurrence.Describe(e));
    }

    /// <summary>The event for the edit form; null unless the user created it and it isn't cancelled.</summary>
    public async Task<Event?> GetForEditAsync(int eventId, string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId && e.CreatorId == userId && !e.IsCancelled);
    }

    public async Task<List<EventTemplate>> GetTemplatesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.EventTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
    }

    // ------------------------------------------------------------------ changes

    public async Task<int> CreateAsync(string userId, EventFormModel form)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var e = new Event { CreatorId = userId, Source = EventSource.UserCreated };
        form.ApplyTo(e);

        db.Events.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    public async Task UpdateAsync(string userId, int eventId, EventFormModel form)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var e = await db.Events.FirstOrDefaultAsync(x => x.Id == eventId && x.CreatorId == userId && !x.IsCancelled)
                ?? throw new InvalidOperationException("Event not found, or you're not its creator.");

        form.ApplyTo(e);
        await NotifyParticipantsAsync(db, e, userId, NotificationType.EventUpdated, $"\"{e.Name}\" was updated.");
        await db.SaveChangesAsync();
    }

    /// <summary>Cancels an event for everyone. It stays in the database (for the search
    /// log and old notifications) but disappears from every calendar.</summary>
    public async Task CancelAsync(string userId, int eventId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var e = await db.Events.FirstOrDefaultAsync(x => x.Id == eventId && x.CreatorId == userId && !x.IsCancelled);
        if (e is null) return;

        e.IsCancelled = true;
        e.CancelledAtUtc = DateTime.UtcNow;
        await NotifyParticipantsAsync(db, e, userId, NotificationType.EventCancelled, $"\"{e.Name}\" was cancelled.");
        await db.SaveChangesAsync();
    }

    /// <summary>Sets my status on someone else's event: Going, Interested, or null to leave.</summary>
    public async Task SetParticipationAsync(string userId, int eventId, ParticipationStatus? status)
    {
        var connectionIds = await connections.GetConnectionIdsAsync(userId);
        await using var db = await dbFactory.CreateDbContextAsync();

        var canSee = await VisibleTo(db, userId, connectionIds)
            .AnyAsync(e => e.Id == eventId && e.CreatorId != userId);
        if (!canSee) throw new InvalidOperationException("You can't join this event.");

        var row = await db.EventParticipations.FindAsync(eventId, userId);

        if (status is null)
        {
            if (row is not null) db.EventParticipations.Remove(row);
        }
        else if (row is null)
        {
            db.EventParticipations.Add(new EventParticipation { EventId = eventId, UserId = userId, Status = status.Value });
        }
        else
        {
            row.Status = status.Value;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private async Task NotifyParticipantsAsync(
        ApplicationDbContext db, Event e, string actorId, NotificationType type, string message)
    {
        var participantIds = await db.EventParticipations
            .Where(p => p.EventId == e.Id && p.UserId != actorId)
            .Select(p => p.UserId)
            .ToListAsync();

        foreach (var id in participantIds)
            await notifications.AddAsync(db, id, type, message, actorId: actorId, eventId: e.Id);
    }
}
