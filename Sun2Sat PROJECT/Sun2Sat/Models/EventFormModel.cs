using System.ComponentModel.DataAnnotations;
using Sun2Sat.Services;

namespace Sun2Sat.Models;

/// <summary>What the Add/Edit event form edits. Times here are LOCAL (what the user
/// typed); EventService converts them to UTC when saving. Keeping this separate from
/// the Event entity means the form can't accidentally change CreatorId, IsCancelled, etc.</summary>
public class EventFormModel : IValidatableObject
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [Display(Name = "Start")]
    public DateTime StartLocal { get; set; }

    [Display(Name = "End")]
    public DateTime EndLocal { get; set; }

    public EventVisibility Visibility { get; set; } = EventVisibility.Connections;

    public RecurrenceFrequency Recurrence { get; set; } = RecurrenceFrequency.None;

    [Range(1, 52, ErrorMessage = "Repeat every 1 to 52.")]
    public int RecurrenceInterval { get; set; } = 1;

    public DaysOfWeekMask RecurrenceDays { get; set; } = DaysOfWeekMask.None;

    [Display(Name = "Repeat until")]
    public DateOnly? RecurrenceUntil { get; set; }

    public int? TemplateId { get; set; }

    public bool HasDay(DayOfWeek day) => RecurrenceDays.HasFlag(Services.Recurrence.DayMask(day));

    public void SetDay(DayOfWeek day, bool on)
    {
        var flag = Services.Recurrence.DayMask(day);
        RecurrenceDays = on ? RecurrenceDays | flag : RecurrenceDays & ~flag;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndLocal <= StartLocal)
            yield return new ValidationResult("End must be after start.", new[] { nameof(EndLocal) });

        if (Recurrence == RecurrenceFrequency.Weekly && RecurrenceDays == DaysOfWeekMask.None)
            yield return new ValidationResult("Pick at least one day.", new[] { nameof(RecurrenceDays) });

        if (Recurrence != RecurrenceFrequency.None && RecurrenceUntil is { } until &&
            until < DateOnly.FromDateTime(StartLocal))
            yield return new ValidationResult("Repeat-until must be on or after the start date.",
                new[] { nameof(RecurrenceUntil) });
    }

    // ---- conversions ----

    public static EventFormModel FromEvent(Event e) => new()
    {
        Name = e.Name,
        Description = e.Description,
        Location = e.Location,
        StartLocal = AppTime.ToLocal(e.StartUtc),
        EndLocal = AppTime.ToLocal(e.EndUtc),
        Visibility = e.Visibility,
        Recurrence = e.Recurrence,
        RecurrenceInterval = e.RecurrenceInterval,
        RecurrenceDays = e.RecurrenceDays,
        RecurrenceUntil = e.RecurrenceUntilUtc is { } u ? DateOnly.FromDateTime(AppTime.ToLocal(u)) : null,
        TemplateId = e.TemplateId
    };

    /// <summary>Copies the form onto an Event (new or existing), converting to UTC.</summary>
    public void ApplyTo(Event e)
    {
        e.Name = Name.Trim();
        e.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        e.Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim();
        e.StartUtc = AppTime.ToUtc(StartLocal);
        e.EndUtc = AppTime.ToUtc(EndLocal);
        e.Visibility = Visibility;
        e.Recurrence = Recurrence;
        e.RecurrenceInterval = Recurrence == RecurrenceFrequency.None ? 1 : RecurrenceInterval;
        e.RecurrenceDays = Recurrence == RecurrenceFrequency.Weekly ? RecurrenceDays : DaysOfWeekMask.None;

        // "Until Dec 31" means through the end of that day, local time.
        e.RecurrenceUntilUtc = Recurrence != RecurrenceFrequency.None && RecurrenceUntil is { } until
            ? AppTime.ToUtc(until.ToDateTime(new TimeOnly(23, 59, 59)))
            : null;

        e.TemplateId = TemplateId;
    }
}
