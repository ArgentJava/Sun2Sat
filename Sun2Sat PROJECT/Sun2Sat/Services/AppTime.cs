namespace Sun2Sat.Services;

/// <summary>Converts between UTC (how the database stores times) and the local time
/// people see and type. The app runs locally, so "local" is the computer's time zone.
/// If you host it later, change Zone to each user's zone (e.g. read from the browser)
/// and nothing else needs to change.</summary>
public static class AppTime
{
    public static TimeZoneInfo Zone => TimeZoneInfo.Local;

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // A time inside the spring-forward gap (e.g. 2:30 AM) doesn't exist; nudge it forward an hour.
        if (Zone.IsInvalidTime(unspecified))
            unspecified = unspecified.AddHours(1);

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone);
    }

    public static DateTime NowLocal => ToLocal(DateTime.UtcNow);

    /// <summary>The Sunday that starts the week containing the given local date.</summary>
    public static DateTime WeekStart(DateTime localDate) =>
        localDate.Date.AddDays(-(int)localDate.DayOfWeek);
}
