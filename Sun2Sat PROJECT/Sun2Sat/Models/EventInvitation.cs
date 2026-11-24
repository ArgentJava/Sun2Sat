using Sun2Sat.Data;

namespace Sun2Sat.Models;

/// <summary>A connection inviting another connection to an event.
/// If the event overlaps one of the recipient's UnavailableBlocks, the invite is saved
/// as AutoDeclinedUnavailable and the sender gets a RecipientUnavailable notification.</summary>
public class EventInvitation
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;

    public string RecipientId { get; set; } = string.Empty;
    public ApplicationUser Recipient { get; set; } = null!;

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
}
