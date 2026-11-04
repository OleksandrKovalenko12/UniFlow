namespace UniFlow.Infrastructure.Time;

// The system stores UTC; dates shown to people (and the year in numbers) are Polish time
public static class PolishTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static DateTime FromUtc(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Time must be in UTC.", nameof(utc));

        return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
    }
}