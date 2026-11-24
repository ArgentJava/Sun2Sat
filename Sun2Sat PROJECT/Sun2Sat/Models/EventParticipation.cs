using Sun2Sat.Data;

namespace Sun2Sat.Models;

/// <summary>Links a user to an event they are Interested in or Going to.
/// One row per (event, user): switching status updates the row, leaving deletes it.
/// For a recurring event this applies to the whole series.</summary>
public class EventParticipation
{
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public ParticipationStatus Status { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
