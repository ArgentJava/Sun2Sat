using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Sun2Sat.Models;

namespace Sun2Sat.Data;

// Replaces the empty ApplicationUser the Blazor template generates.
// IdentityUser already provides: Id, UserName (the login name), Email,
// PhoneNumber and PasswordHash, so we only add what Sun2Sat needs on top.
public class ApplicationUser : IdentityUser
{
    /// <summary>Optional friendly name shown alongside the username on the profile.</summary>
    [MaxLength(60)]
    public string? DisplayName { get; set; }

    /// <summary>The in-app notification toggle.</summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>What to show in lists: the display name if set, otherwise the username.</summary>
    [NotMapped]
    public string NameToShow =>
        string.IsNullOrWhiteSpace(DisplayName) ? UserName ?? string.Empty : DisplayName;

    // Navigation properties
    public List<Event> CreatedEvents { get; set; } = new();
    public List<EventParticipation> Participations { get; set; } = new();
    public List<UserConnection> SentConnectionRequests { get; set; } = new();
    public List<UserConnection> ReceivedConnectionRequests { get; set; } = new();
    public List<UnavailableBlock> UnavailableBlocks { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
}
