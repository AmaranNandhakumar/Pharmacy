namespace Pharmacy.Api.Services;

/// <summary>
/// The pharmacy works in local dates ("today's sales"), while rows are stored in UTC.
/// These helpers convert between the two using the server's local time zone.
/// </summary>
public static class TimeProviderExtensions
{
    public static DateOnly LocalToday(this TimeProvider time) => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    public static DateOnly ToLocalDate(this TimeProvider time, DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), time.LocalTimeZone));

    /// <summary>A range of local dates as [start, end) in UTC; both days are included.</summary>
    public static (DateTime start, DateTime end) UtcRange(this TimeProvider time, DateOnly from, DateOnly to)
    {
        var zone = time.LocalTimeZone;
        var start = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        return (start, end);
    }
}
