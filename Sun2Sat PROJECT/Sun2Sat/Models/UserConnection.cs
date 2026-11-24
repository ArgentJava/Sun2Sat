using Sun2Sat.Data;

namespace Sun2Sat.Models;

/// <summary>A connection request between two users ("friending").
/// Only rows with Status == Accepted count as a connection. Removing a connection
/// deletes the row, so the other user's events disappear from your calendar
/// immediately (this covers the "events from a connection you no longer share" glitch).</summary>
public class UserConnection
{
    public int Id { get; set; }

    public string RequesterId { get; set; } = string.Empty;
    public ApplicationUser Requester { get; set; } = null!;

    public string AddresseeId { get; set; } = string.Empty;
    public ApplicationUser Addressee { get; set; } = null!;

    public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
}
