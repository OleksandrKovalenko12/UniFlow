namespace UniFlow.Shared.Applications;

// One list for the browser (file picker, early checks) and the server
public static class AttachmentRules
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    // Lower case with the dot, as Path.GetExtension returns them
    public static readonly IReadOnlyList<string> Extensions =
        [".pdf", ".docx", ".odt", ".jpg", ".jpeg", ".png", ".webp"];

    public static bool IsAllowedExtension(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());
}