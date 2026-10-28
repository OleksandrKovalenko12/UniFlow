namespace UniFlow.Domain;

public sealed record StoredFile(string OriginalFileName, string ContentType,
    string StoragePath, string? PdfStoragePath, long SizeBytes);