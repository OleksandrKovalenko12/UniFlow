using UniFlow.Domain.Enums;

namespace UniFlow.Domain.Entities;

public class Attachment
{
    public const string PdfContentType = "application/pdf";
    public const long MaxSizeBytes = 10 * 1024 * 1024;   
    private const int MaxFileNameLength = 255;

    private static readonly string[] AllowedContentTypes =
    [
        PdfContentType,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "image/jpeg",
        "image/png",
    ];

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public AttachmentKind Kind { get; private set; }
    public string OriginalFileName { get; private set; } = null!;
    public string OriginalContentType { get; private set; } = null!;
    public string StoragePath { get; private set; } = null!;
    public string? PdfStoragePath { get; private set; }
    public long SizeBytes { get; private set; }
    public string UploadedById { get; private set; } = null!;
    public DateTime UploadedAt { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    internal Attachment(AttachmentKind kind, StoredFile file, string uploadedById,
        DateTime uploadedAt)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedById);

        if (string.IsNullOrWhiteSpace(file.StoragePath))
            throw new ArgumentException("Storage path is required.", nameof(file));
        if (uploadedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Time must be in UTC.", nameof(uploadedAt));

        var fileName = NormalizeFileName(file.OriginalFileName);

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new ArgumentException($"Content type '{file.ContentType}' is not allowed.",
                nameof(file));
        if (kind == AttachmentKind.GeneratedDocument && file.ContentType != PdfContentType)
            throw new ArgumentException("Generated document must be a PDF.", nameof(file));

        var isPdf = file.ContentType == PdfContentType;

        if (isPdf && file.PdfStoragePath is not null)
            throw new ArgumentException("PDF file must not have a separate PDF version.",
                nameof(file));
        if (!isPdf && string.IsNullOrWhiteSpace(file.PdfStoragePath))
            throw new ArgumentException("Converted PDF version is required.", nameof(file));

        if (file.SizeBytes < 1 || file.SizeBytes > MaxSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(file),
                $"File size must be between 1 byte and {MaxSizeBytes} bytes.");

        Id = Guid.CreateVersion7();
        Kind = kind;
        OriginalFileName = fileName;
        OriginalContentType = file.ContentType;
        StoragePath = file.StoragePath;
        PdfStoragePath = file.PdfStoragePath;
        SizeBytes = file.SizeBytes;
        UploadedById = uploadedById;
        UploadedAt = uploadedAt;
        IsArchived = false;
    }

    private Attachment() { }

    private static string NormalizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", "file");

        var trimmed = fileName.Trim();

        if (trimmed.Length > MaxFileNameLength)
            throw new ArgumentException($"File name must be at most {MaxFileNameLength} " +
                "characters long.", "file");

        return trimmed;
    }

    internal void Archive(DateTime now)
    {
        if (IsArchived)
            throw new InvalidOperationException("Attachment is already archived.");
        if (now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Time must be in UTC.", nameof(now));

        IsArchived = true;
        ArchivedAt = now;
    }
}