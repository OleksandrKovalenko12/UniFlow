namespace UniFlow.Domain.Entities;

// A file in a draft: already checked and converted, waiting for the application to be submitted
public class DraftFile
{
    public Guid Id { get; private set; }
    public Guid DraftId { get; private set; }
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public string StoragePath { get; private set; } = null!;
    public string? PdfStoragePath { get; private set; }
    public long SizeBytes { get; private set; }
    public DateTime UploadedAt { get; private set; }

    internal DraftFile(StoredFile file, DateTime uploadedAt)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.OriginalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.StoragePath);

        Id = Guid.CreateVersion7();
        OriginalFileName = file.OriginalFileName;
        ContentType = file.ContentType;
        StoragePath = file.StoragePath;
        PdfStoragePath = file.PdfStoragePath;
        SizeBytes = file.SizeBytes;
        UploadedAt = uploadedAt;
    }

    private DraftFile() { }

    // On submission the same file on disk becomes an attachment of the application
    public StoredFile ToStoredFile() =>
        new(OriginalFileName, ContentType, StoragePath, PdfStoragePath, SizeBytes);
}