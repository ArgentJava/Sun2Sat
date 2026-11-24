using Microsoft.EntityFrameworkCore;
using Sun2Sat.Data;
using Sun2Sat.Models;

namespace Sun2Sat.Services;

/// <summary>Everything about connecting with other users.
///
/// Rules:
///  - There is at most ONE UserConnection row per pair of users, whichever direction.
///  - Only Accepted rows count as a connection.
///  - Ignoring a request hides it from the addressee; the requester keeps seeing
///    "Request sent" and is never told.
///  - Removing a connection or cancelling a request deletes the row.
///
/// Uses IDbContextFactory and a fresh DbContext per method. That's the recommended
/// pattern for interactive Blazor Server, where one component can live for a long time
/// and a shared DbContext would go stale or be used by two calls at once.</summary>
public class ConnectionService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    NotificationService notifications)
{
    // ------------------------------------------------------------------ queries

    /// <summary>Finds users whose username or display name contains the search text.</summary>
    public async Task<List<UserWithRelationship>> SearchUsersAsync(string currentUserId, string query, int max = 20)
    {
        query = query.Trim();
        if (query.Length < 2) return new();

        // Escape LIKE wildcards so searching for "_" or "%" means the literal character.
        var pattern = "%" + query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        await using var db = await dbFactory.CreateDbContextAsync();

        // SQLite's LIKE is case-insensitive for English letters, which is what we want.
        var users = await db.Users
            .Where(u => u.Id != currentUserId &&
                        (EF.Functions.Like(u.UserName!, pattern, "\\") ||
                         (u.DisplayName != null && EF.Functions.Like(u.DisplayName, pattern, "\\"))))
            .OrderBy(u => u.UserName)
            .Take(max)
            .Select(u => new UserSummary(u.Id, u.UserName!, u.DisplayName))
            .ToListAsync();

        var ids = users.Select(u => u.Id).ToList();
        var rows = await RowsBetweenAsync(db, currentUserId, ids);

        return users
            .Select(u => new UserWithRelationship(u, StateFor(currentUserId, rows.GetValueOrDefault(u.Id))))
            .ToList();
    }

    /// <summary>Looks up one user by username for their profile page. Null if not found.</summary>
    public async Task<UserWithRelationship?> GetByUserNameAsync(string currentUserId, string userName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var normalized = userName.Trim().ToUpperInvariant();
        var user = await db.Users
            .Where(u => u.NormalizedUserName == normalized)
            .Select(u => new UserSummary(u.Id, u.UserName!, u.DisplayName))
            .FirstOrDefaultAsync();

        if (user is null) return null;
        if (user.Id == currentUserId) return new(user, RelationshipState.Self);

        var row = await FindRowAsync(db, currentUserId, user.Id);
        return new(user, StateFor(currentUserId, row));
    }

    public async Task<UserSummary?> GetSummaryAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserSummary(u.Id, u.UserName!, u.DisplayName))
            .FirstOrDefaultAsync();
    }

    /// <summary>People I'm connected to, sorted by name.</summary>
    public async Task<List<UserSummary>> GetConnectionsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var list = await db.Connections
            .Where(c => c.Status == ConnectionStatus.Accepted &&
                        (c.RequesterId == userId || c.AddresseeId == userId))
            .Select(c => c.RequesterId == userId
                ? new UserSummary(c.Addressee.Id, c.Addressee.UserName!, c.Addressee.DisplayName)
                : new UserSummary(c.Requester.Id, c.Requester.UserName!, c.Requester.DisplayName))
            .ToListAsync();

        return list.OrderBy(u => u.NameToShow, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>IDs of everyone I'm connected to. Later steps use this to decide whose events I can see.</summary>
    public async Task<List<string>> GetConnectionIdsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Connections
            .Where(c => c.Status == ConnectionStatus.Accepted &&
                        (c.RequesterId == userId || c.AddresseeId == userId))
            .Select(c => c.RequesterId == userId ? c.AddresseeId : c.RequesterId)
            .ToListAsync();
    }

    public async Task<bool> AreConnectedAsync(string userA, string userB)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Connections.AnyAsync(c =>
            c.Status == ConnectionStatus.Accepted &&
            ((c.RequesterId == userA && c.AddresseeId == userB) ||
             (c.RequesterId == userB && c.AddresseeId == userA)));
    }

    /// <summary>Requests waiting on me. Ignored ones are left out (that's what "ignore" does).</summary>
    public async Task<List<ConnectionRequestView>> GetIncomingRequestsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Connections
            .Where(c => c.AddresseeId == userId && c.Status == ConnectionStatus.Pending)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new ConnectionRequestView(
                c.Id,
                new UserSummary(c.Requester.Id, c.Requester.UserName!, c.Requester.DisplayName),
                c.CreatedAtUtc))
            .ToListAsync();
    }

    /// <summary>Requests I sent that aren't accepted yet. Includes ignored ones, which
    /// look the same as pending to the sender.</summary>
    public async Task<List<ConnectionRequestView>> GetOutgoingRequestsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Connections
            .Where(c => c.RequesterId == userId && c.Status != ConnectionStatus.Accepted)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new ConnectionRequestView(
                c.Id,
                new UserSummary(c.Addressee.Id, c.Addressee.UserName!, c.Addressee.DisplayName),
                c.CreatedAtUtc))
            .ToListAsync();
    }

    public async Task<int> CountIncomingRequestsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Connections.CountAsync(c =>
            c.AddresseeId == userId && c.Status == ConnectionStatus.Pending);
    }

    // ------------------------------------------------------------------ actions

    /// <summary>Sends a connection request, handling every existing state between the two users.
    /// Returns the new state.</summary>
    public async Task<RelationshipState> SendRequestAsync(string fromUserId, string toUserId)
    {
        if (fromUserId == toUserId) return RelationshipState.Self;

        await using var db = await dbFactory.CreateDbContextAsync();

        if (!await db.Users.AnyAsync(u => u.Id == toUserId))
            throw new InvalidOperationException("That user no longer exists.");

        var row = await FindRowAsync(db, fromUserId, toUserId);

        if (row is null)
        {
            db.Connections.Add(new UserConnection { RequesterId = fromUserId, AddresseeId = toUserId });
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two clicks at once hit the unique index. The request exists either way.
            }
            return RelationshipState.RequestSent;
        }

        switch (row.Status)
        {
            case ConnectionStatus.Accepted:
                return RelationshipState.Connected;

            case ConnectionStatus.Pending when row.RequesterId == fromUserId:
                return RelationshipState.RequestSent;

            case ConnectionStatus.Pending:
                // They already asked me, so asking them back counts as accepting.
                await AcceptRowAsync(db, row);
                return RelationshipState.Connected;

            case ConnectionStatus.Ignored when row.RequesterId == fromUserId:
                // They ignored me. Don't resend or nag them, and don't reveal the ignore.
                return RelationshipState.RequestSent;

            case ConnectionStatus.Ignored:
                // I ignored them earlier and changed my mind: turn it into a fresh request from me.
                row.RequesterId = fromUserId;
                row.AddresseeId = toUserId;
                row.Status = ConnectionStatus.Pending;
                row.CreatedAtUtc = DateTime.UtcNow;
                row.RespondedAtUtc = null;
                await db.SaveChangesAsync();
                return RelationshipState.RequestSent;

            default:
                return RelationshipState.None;
        }
    }

    /// <summary>Accepts a request addressed to me.</summary>
    public async Task AcceptAsync(string currentUserId, int connectionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.Connections.FirstOrDefaultAsync(c =>
            c.Id == connectionId && c.AddresseeId == currentUserId && c.Status == ConnectionStatus.Pending);

        if (row is not null) await AcceptRowAsync(db, row);
    }

    /// <summary>Ignores a request addressed to me: it disappears from my requests and feed.</summary>
    public async Task IgnoreAsync(string currentUserId, int connectionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.Connections.FirstOrDefaultAsync(c =>
            c.Id == connectionId && c.AddresseeId == currentUserId && c.Status == ConnectionStatus.Pending);

        if (row is null) return;
        row.Status = ConnectionStatus.Ignored;
        row.RespondedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Cancels a request I sent (pending or silently ignored).</summary>
    public async Task CancelRequestAsync(string currentUserId, int connectionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Connections
            .Where(c => c.Id == connectionId && c.RequesterId == currentUserId && c.Status != ConnectionStatus.Accepted)
            .ExecuteDeleteAsync();
    }

    /// <summary>Removes an accepted connection. Both people stop seeing each other's
    /// connections-only events immediately, because those queries check for an Accepted row.</summary>
    public async Task RemoveConnectionAsync(string currentUserId, string otherUserId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Connections
            .Where(c => c.Status == ConnectionStatus.Accepted &&
                        ((c.RequesterId == currentUserId && c.AddresseeId == otherUserId) ||
                         (c.RequesterId == otherUserId && c.AddresseeId == currentUserId)))
            .ExecuteDeleteAsync();
    }

    // ------------------------------------------------------------------ helpers

    private async Task AcceptRowAsync(ApplicationDbContext db, UserConnection row)
    {
        row.Status = ConnectionStatus.Accepted;
        row.RespondedAtUtc = DateTime.UtcNow;

        var accepter = await db.Users
            .Where(u => u.Id == row.AddresseeId)
            .Select(u => new UserSummary(u.Id, u.UserName!, u.DisplayName))
            .FirstAsync();

        await notifications.AddAsync(db, row.RequesterId, NotificationType.ConnectionAccepted,
            $"{accepter.NameToShow} accepted your connection request.", actorId: accepter.Id);

        await db.SaveChangesAsync();
    }

    private static Task<UserConnection?> FindRowAsync(ApplicationDbContext db, string a, string b) =>
        db.Connections.FirstOrDefaultAsync(c =>
            (c.RequesterId == a && c.AddresseeId == b) ||
            (c.RequesterId == b && c.AddresseeId == a));

    /// <summary>All rows between me and a set of users, keyed by the other user's id.</summary>
    private static async Task<Dictionary<string, UserConnection>> RowsBetweenAsync(
        ApplicationDbContext db, string me, List<string> others)
    {
        var rows = await db.Connections
            .Where(c => (c.RequesterId == me && others.Contains(c.AddresseeId)) ||
                        (c.AddresseeId == me && others.Contains(c.RequesterId)))
            .ToListAsync();

        // GroupBy guards against a duplicate pair slipping in; the service never creates one.
        return rows
            .GroupBy(r => r.RequesterId == me ? r.AddresseeId : r.RequesterId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private static RelationshipState StateFor(string me, UserConnection? row) => row switch
    {
        null => RelationshipState.None,
        { Status: ConnectionStatus.Accepted } => RelationshipState.Connected,
        { Status: ConnectionStatus.Pending } when row.RequesterId == me => RelationshipState.RequestSent,
        { Status: ConnectionStatus.Pending } => RelationshipState.RequestReceived,
        // Ignored: the sender still sees "sent"; the person who ignored sees nothing pending.
        { Status: ConnectionStatus.Ignored } when row.RequesterId == me => RelationshipState.RequestSent,
        _ => RelationshipState.None
    };
}
