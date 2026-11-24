using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Sun2Sat.Data;

/// <summary>SQLite has no real date type, so EF reads DateTimes back with
/// Kind = Unspecified. This converter makes sure everything goes in as UTC and
/// comes back out marked as UTC, so .ToLocalTime() in the UI behaves correctly.</summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}
