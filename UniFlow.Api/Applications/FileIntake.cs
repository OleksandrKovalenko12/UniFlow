using UniFlow.Api.Errors;
using UniFlow.Domain;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Pdf;
using UniFlow.Infrastructure.Storage;
using UniFlow.Shared.Applications;

namespace UniFlow.Api.Applications;

// Takes in one file sent by a student: checks it, saves it and converts it to PDF.
// When anything fails, nothing is left on disk.
public sealed class FileIntake
{
    private readonly IPdfConverter _pdf;
    private readonly IFileStorage _storage;

    public FileIntake(IPdfConverter pdf, IFileStorage storage)
    {
        _pdf = pdf;
        _storage = storage;
    }

    public async Task<StoredFile> StoreAsync(IFormFile file, string folder,
        CancellationToken cancellationToken)
    {
        if (file.Length < 1 || file.Length > AttachmentRules.MaxSizeBytes)
            throw new InvalidFileException(ErrorMapping.InvalidFileSize,
                $"File size {file.Length} B is outside the allowed range.");

        // Some browsers send a full path - only the name is kept
        var fileName = Path.GetFileName(file.FileName);
        var type = FileTypes.Detect(fileName, await ReadHeaderAsync(file, cancellationToken))
            ?? throw new InvalidFileException(ErrorMapping.UnsupportedFileType,
                $"'{fileName}' is not an allowed file or its content does not match the name.");

        string? originalPath = null;
        string? pdfPath = null;

        try
        {
            await using (var content = file.OpenReadStream())
                originalPath = await _storage.SaveAsync(content, folder, type.Extension,
                    cancellationToken);

            // A PDF is kept as it is; anything else gets a PDF version for viewing
            if (type.ContentType != Attachment.PdfContentType)
                pdfPath = await ConvertAsync(originalPath, type.ContentType, folder,
                    cancellationToken);

            return new StoredFile(fileName, type.ContentType, originalPath, pdfPath, file.Length);
        }
        catch
        {
            Delete(originalPath, pdfPath);
            throw;
        }
    }

    // For a file that was stored but is not wanted after all
    public void Delete(StoredFile file) => Delete(file.StoragePath, file.PdfStoragePath);

    private void Delete(string? originalPath, string? pdfPath)
    {
        if (pdfPath is not null)
            _storage.Delete(pdfPath);
        if (originalPath is not null)
            _storage.Delete(originalPath);
    }

    private static async Task<byte[]> ReadHeaderAsync(IFormFile file,
        CancellationToken cancellationToken)
    {
        var header = new byte[FileTypes.HeaderLength];
        await using var content = file.OpenReadStream();
        var read = await content.ReadAtLeastAsync(header, header.Length,
            throwOnEndOfStream: false, cancellationToken);

        return header[..read];
    }

    private async Task<string> ConvertAsync(string originalPath, string contentType,
        string folder, CancellationToken cancellationToken)
    {
        byte[] pdf;

        try
        {
            // The converter closes the stream it gets
            pdf = await _pdf.ConvertToPdfAsync(_storage.OpenRead(originalPath), contentType,
                cancellationToken);
        }
        catch (PdfConversionException exception) when (exception.FileRejected)
        {
            throw new InvalidFileException(ErrorMapping.FileConversionFailed, exception.Message);
        }

        using var content = new MemoryStream(pdf);
        return await _storage.SaveAsync(content, folder, ".pdf", cancellationToken);
    }
}