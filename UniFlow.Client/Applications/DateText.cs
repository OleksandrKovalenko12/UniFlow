using System.Globalization;

namespace UniFlow.Client.Applications;

// The server keeps UTC; people read the time of their own browser (Polish time in Poland)
public static class DateText
{
    // "12.11.2026 11:39"
    public static string Format(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime()
            .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    // "12.11.2026"
    public static string Day(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime()
            .ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}