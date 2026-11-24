using System.ComponentModel.DataAnnotations;
using Sun2Sat.Data;

namespace Sun2Sat.Models;

/// <summary>An in-app notification. Nothing is emailed or texted in this version.
/// If the recipient has NotificationsEnabled == false, the service layer simply
/// doesn't create the row.</summary>
public class Notification
{
    public int Id { get; set; }

    public string RecipientId { get; set; } = string.Empty;
    public ApplicationUser Recipient { get; set; } = null!;

    public NotificationType Type { get; set; }

    [Required, MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    /// <summary>The user who caused this notification (sent the invite, accepted the request...).</summary>
    public string? ActorId { get; set; }
    public ApplicationUser? Actor { get; set; }

    public int? EventId { get; set; }
    public Event? Event { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
