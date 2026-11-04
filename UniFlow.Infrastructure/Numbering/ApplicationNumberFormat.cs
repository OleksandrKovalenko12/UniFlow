using System.Globalization;

namespace UniFlow.Infrastructure.Numbering;

public static class ApplicationNumberFormat
{
    public const string Prefix = "UF";

    // UF/2026/0042 - at least 4 digits, more when the year has over 9999 applications
    public static string Format(int year, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);

        return string.Create(CultureInfo.InvariantCulture, $"{Prefix}/{year}/{sequence:D4}");
    }
}