using System.Globalization;

namespace UniFlow.Client.Applications;

public static class FileSizeText
{
    // Polish decimal comma, whatever language the browser is set to
    private static readonly NumberFormatInfo Comma = new() { NumberDecimalSeparator = "," };

    public static string Format(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{Math.Ceiling(bytes / 1024d)} KB",
        _ => (bytes / 1024d / 1024).ToString("0.0", Comma) + " MB"
    };
}