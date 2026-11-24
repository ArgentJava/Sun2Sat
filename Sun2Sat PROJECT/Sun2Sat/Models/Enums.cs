namespace Sun2Sat.Models;

/// <summary>Who can see an event. There is deliberately no "Private" option;
/// users block personal time with an UnavailableBlock instead.</summary>
public enum EventVisibility
{
    Connections = 0,
    Public = 1
}

/// <summary>Where an event came from.</summary>
public enum EventSource
{
    UserCreated = 0,
    /// <summary>Officiated events: concerts at a known venue, state fair, etc.</summary>
    Official = 1
}

public enum RecurrenceFrequency
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

/// <summary>Bit flags so one Weekly event can repeat on several days (e.g. Mon + Wed + Fri).</summary>
[Flags]
public enum DaysOfWeekMask
{
    None = 0,
    Sunday = 1,
    Monday = 2,
    Tuesday = 4,
    Wednesday = 8,
    Thursday = 16,
    Friday = 32,
    Saturday = 64
}

/// <summary>Interested = saved to my calendar, not shown as an attendee.
/// Going = shown in the attendee list on the event page.</summary>
public enum ParticipationStatus
{
    Interested = 0,
    Going = 1
}

public enum ConnectionStatus
{
    Pending = 0,
    Accepted = 1,
    /// <summary>Removed from the addressee's feed; the requester is not told.</summary>
    Ignored = 2
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    /// <summary>Invite overlapped one of the recipient's Unavailable blocks.</summary>
    AutoDeclinedUnavailable = 3
}

public enum NotificationType
{
    ConnectionRequest = 0,
    ConnectionAccepted = 1,
    EventInvitation = 2,
    InvitationAccepted = 3,
    InvitationDeclined = 4,
    RecipientUnavailable = 5,
    EventCancelled = 6,
    EventUpdated = 7
}
