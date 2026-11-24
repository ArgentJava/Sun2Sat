using Sun2Sat.Models;

namespace Sun2Sat.Services;

/// <summary>Why an event is on my calendar. Drives its color and label.</summary>
public enum CalendarItemKind
{
    Mine,        // I created it
    Going,       // someone else's event; I'm attending
    Interested,  // someone else's event; saved as interested
    Connection   // a connection's event I haven't responded to
}

/// <summary>One occurrence on the calendar. A weekly event produces one of these per week.</summary>
public record CalendarItem(
    int EventId,
    string Name,
    string? Location,
    DateTime StartUtc,
    DateTime EndUtc,
    CalendarItemKind Kind,
    UserSummary? Host,
    bool IsRecurring)
{
    public DateTime StartLocal => AppTime.ToLocal(StartUtc);
    public DateTime EndLocal => AppTime.ToLocal(EndUtc);
}

/// <summary>Everything the event page shows.</summary>
public record EventDetails(
    Event Event,
    UserSummary? Host,
    bool IsHost,
    ParticipationStatus? MyStatus,
    List<UserSummary> Attendees,
    int InterestedCount,
    string RecurrenceText);
