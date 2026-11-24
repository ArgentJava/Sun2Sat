using Sun2Sat.Models;

namespace Sun2Sat.Services;

/// <summary>Turns one stored Event into the individual occurrences that fall inside a
/// time range. Repeating events are stored once and expanded here, when the calendar
/// asks for a specific week.
///
/// The expansion happens in LOCAL time, so "every Monday at 9:00 AM" stays at 9:00 AM
/// across daylight-saving changes, and each occurrence is then converted back to UTC.</summary>
public static class Recurrence
{
    private const int MaxSteps = 2000; // safety net against runaway loops

    public static IEnumerable<(DateTime StartUtc, DateTime EndUtc)> Occurrences(
        Event e, DateTime rangeStartUtc, DateTime rangeEndUtc)
    {
        var duration = e.EndUtc - e.StartUtc;

        if (e.Recurrence == RecurrenceFrequency.None)
        {
            if (e.StartUtc < rangeEndUtc && e.EndUtc > rangeStartUtc)
                yield return (e.StartUtc, e.EndUtc);
            yield break;
        }

        var firstLocal = AppTime.ToLocal(e.StartUtc);
        var rangeStartLocal = AppTime.ToLocal(rangeStartUtc);
        var interval = Math.Max(1, e.RecurrenceInterval);

        foreach (var local in Candidates(e, firstLocal, rangeStartLocal - duration, interval))
        {
            var startUtc = AppTime.ToUtc(local);

            if (startUtc >= rangeEndUtc) yield break;
            if (e.RecurrenceUntilUtc is { } until && startUtc > until) yield break;

            var endUtc = startUtc + duration;
            if (endUtc > rangeStartUtc)
                yield return (startUtc, endUtc);
        }
    }

    /// <summary>Local start times in order, beginning near <paramref name="skipToLocal"/>
    /// so we don't walk through years of old occurrences.</summary>
    private static IEnumerable<DateTime> Candidates(Event e, DateTime firstLocal, DateTime skipToLocal, int interval)
    {
        switch (e.Recurrence)
        {
            case RecurrenceFrequency.Daily:
            {
                var skip = Math.Max(0, (int)Math.Floor((skipToLocal - firstLocal).TotalDays / interval));
                for (var i = skip; i < skip + MaxSteps; i++)
                    yield return firstLocal.AddDays((long)i * interval);
                break;
            }

            case RecurrenceFrequency.Weekly:
            {
                var days = e.RecurrenceDays == DaysOfWeekMask.None
                    ? DayMask(firstLocal.DayOfWeek)
                    : e.RecurrenceDays;

                var firstWeek = AppTime.WeekStart(firstLocal);
                var weeksAhead = Math.Max(0, (int)Math.Floor((skipToLocal - firstWeek).TotalDays / 7));
                var skip = weeksAhead / interval;

                for (var w = skip; w < skip + MaxSteps; w++)
                {
                    var weekStart = firstWeek.AddDays((long)w * interval * 7);
                    for (var d = 0; d < 7; d++)
                    {
                        if (!days.HasFlag(DayMask((DayOfWeek)d))) continue;
                        var candidate = weekStart.AddDays(d) + firstLocal.TimeOfDay;
                        if (candidate >= firstLocal) yield return candidate;
                    }
                }
                break;
            }

            case RecurrenceFrequency.Monthly:
            {
                var monthsAhead = Math.Max(0,
                    (skipToLocal.Year - firstLocal.Year) * 12 + skipToLocal.Month - firstLocal.Month - 1);
                var skip = monthsAhead / interval;

                // AddMonths clamps: an event on the 31st lands on the 30th/28th in shorter months.
                for (var i = skip; i < skip + MaxSteps; i++)
                    yield return firstLocal.AddMonths(i * interval);
                break;
            }
        }
    }

    public static DaysOfWeekMask DayMask(DayOfWeek day) => (DaysOfWeekMask)(1 << (int)day);

    /// <summary>Human-readable summary, e.g. "Every 2 weeks on Mon, Wed until Dec 31, 2026".</summary>
    public static string Describe(Event e)
    {
        if (e.Recurrence == RecurrenceFrequency.None) return "Does not repeat";

        var n = Math.Max(1, e.RecurrenceInterval);
        var text = e.Recurrence switch
        {
            RecurrenceFrequency.Daily => n == 1 ? "Every day" : $"Every {n} days",
            RecurrenceFrequency.Weekly => (n == 1 ? "Every week" : $"Every {n} weeks") + " on " + DayList(e),
            RecurrenceFrequency.Monthly => (n == 1 ? "Every month" : $"Every {n} months") +
                                           $" on day {AppTime.ToLocal(e.StartUtc).Day}",
            _ => ""
        };

        if (e.RecurrenceUntilUtc is { } until)
            text += $" until {AppTime.ToLocal(until):MMM d, yyyy}";

        return text;
    }

    private static string DayList(Event e)
    {
        var mask = e.RecurrenceDays == DaysOfWeekMask.None
            ? DayMask(AppTime.ToLocal(e.StartUtc).DayOfWeek)
            : e.RecurrenceDays;

        var names = Enum.GetValues<DayOfWeek>()
            .Where(d => mask.HasFlag(DayMask(d)))
            .Select(d => d.ToString()[..3]);

        return string.Join(", ", names);
    }
}
