using System.ComponentModel.DataAnnotations;
using Sun2Sat.Data;

namespace Sun2Sat.Models;

public class Event : IValidatableObject
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    // All times are stored in UTC and converted to the viewer's time zone in the UI.
    // For a recurring event these are the times of the FIRST occurrence.
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public EventVisibility Visibility { get; set; } = EventVisibility.Connections;
    public EventSource Source { get; set; } = EventSource.UserCreated;

    /// <summary>Venue or organizer for Official events (e.g. "State Fairgrounds").</summary>
    [MaxLength(200)]
    public string? OrganizerName { get; set; }

    // ---- Recurrence ----
    public RecurrenceFrequency Recurrence { get; set; } = RecurrenceFrequency.None;

    /// <summary>Every N days/weeks/months. 1 = every week, 2 = every other week, etc.</summary>
    [Range(1, 52)]
    public int RecurrenceInterval { get; set; } = 1;

    /// <summary>Only used when Recurrence is Weekly.</summary>
    public DaysOfWeekMask RecurrenceDays { get; set; } = DaysOfWeekMask.None;

    /// <summary>Last date the series runs. Null = repeats indefinitely.</summary>
    public DateTime? RecurrenceUntilUtc { get; set; }

    // ---- Relationships ----

    /// <summary>The general-event template this was made from (Work, Gym...), if any.</summary>
    public int? TemplateId { get; set; }
    public EventTemplate? Template { get; set; }

    /// <summary>Null for Official events seeded by the app.</summary>
    public string? CreatorId { get; set; }
    public ApplicationUser? Creator { get; set; }

    public List<EventParticipation> Participants { get; set; } = new();
    public List<EventInvitation> Invitations { get; set; } = new();

    // ---- Lifecycle ----

    /// <summary>Soft delete: a cancelled event stays in the database (so the search log
    /// and notifications still make sense) but every calendar query filters it out.</summary>
    public bool IsCancelled { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EndUtc <= StartUtc)
            yield return new ValidationResult("End time must be after the start time.",
                new[] { nameof(EndUtc) });

        if (Recurrence == RecurrenceFrequency.Weekly && RecurrenceDays == DaysOfWeekMask.None)
            yield return new ValidationResult("Pick at least one day for a weekly event.",
                new[] { nameof(RecurrenceDays) });

        if (RecurrenceUntilUtc is { } until && until < StartUtc)
            yield return new ValidationResult("The repeat end date must be after the first occurrence.",
                new[] { nameof(RecurrenceUntilUtc) });
    }
}
