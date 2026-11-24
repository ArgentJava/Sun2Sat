using Microsoft.EntityFrameworkCore;
using Sun2Sat.Data;
using Sun2Sat.Models;

namespace Sun2Sat.Services;

/// <summary>Creates in-app notifications. All features go through here so the
/// "notifications off" toggle is respected in one place.</summary>
public class NotificationService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>Adds a notification to the given context (the caller saves it, so the
    /// notification and the change that caused it are written together).
    /// Does nothing if the recipient has notifications turned off.</summary>
    public async Task AddAsync(
        ApplicationDbContext db,
        string recipientId,
        NotificationType type,
        string message,
        string? actorId = null,
        int? eventId = null)
    {
        var enabled = await db.Users
            .Where(u => u.Id == recipientId)
            .Select(u => u.NotificationsEnabled)
            .FirstOrDefaultAsync();

        if (!enabled) return;

        db.Notifications.Add(new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Message = message,
            ActorId = actorId,
            EventId = eventId
        });
    }

    public async Task<int> CountUnreadAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);
    }
}
