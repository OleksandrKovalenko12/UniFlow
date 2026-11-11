namespace UniFlow.Api.Applications;

public sealed record DetectedFileType(string ContentType, string Extension);

// The server decides the file type itself: an extension from the list and the file's first
// bytes must agree. The browser's Content-Type is not trusted (Windows without Office sends
// DOCX as application/octet-stream, and anyone can send anything).
public static class FileTypes
{
    // Enough for every signature below (WebP: "RIFF", 4 bytes of size, "WEBP")
    public const int HeaderLength = 12;

    private const string Docx =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Odt = "application/vnd.oasis.opendocument.text";

    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];   // DOCX and ODT are ZIP files
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] WebP = "WEBP"u8.ToArray();

    // Null when the extension is not allowed or the content is not what the name says
    public static DetectedFileType? Detect(string fileName, ReadOnlySpan<byte> header) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" when header.StartsWith(Pdf) => new("application/pdf", ".pdf"),
            ".docx" when header.StartsWith(Zip) => new(Docx, ".docx"),
            ".odt" when header.StartsWith(Zip) => new(Odt, ".odt"),
            ".jpg" or ".jpeg" when header.StartsWith(Jpeg) => new("image/jpeg", ".jpg"),
            ".png" when header.StartsWith(Png) => new("image/png", ".png"),
            ".webp" when header.Length >= HeaderLength && header.StartsWith(Riff)
                && header[8..].StartsWith(WebP) => new("image/webp", ".webp"),
            _ => null
        };
}