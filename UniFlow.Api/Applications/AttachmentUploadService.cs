using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Errors;
using UniFlow.Domain;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Pdf;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Infrastructure.Storage;
using UniFlow.Shared.Applications;

namespace UniFlow.Api.Applications;

public sealed class AttachmentUploadService
{
    private readonly UniFlowDbContext _db;
    private readonly IPdfConverter _pdf;
    private readonly IFileStorage _storage;

    public AttachmentUploadService(UniFlowDbContext db, IPdfConverter pdf, IFileStorage storage)
    {
        _db = db;
        _pdf = pdf;
        _storage = storage;
    }

    // Null when the application does not exist or belongs to someone else
    public async Task<AttachmentSummary?> UploadAsync(string userId, Guid applicationId,
        IFormFile file, CancellationToken cancellationToken)
    {
        var application = await _db.Applications
            .Include(a => a.Attachments)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.ApplicantId == userId,
                cancellationToken);

        if (application is null)
            return null;

        var actor = new Actor(userId, ActorRole.Student);

        // Cheap checks first: saving and converting take seconds
        application.EnsureCanAddAttachment(actor);

        if (file.Length < 1 || file.Length > AttachmentRules.MaxSizeBytes)
            throw new InvalidFileException(ErrorMapping.InvalidFileSize,
                $"File size {file.Length} B is outside the allowed range.");

        // Some browsers send a full path - only the name is kept
        var fileName = Path.GetFileName(file.FileName);
        var type = FileTypes.Detect(fileName, await ReadHeaderAsync(file, cancellationToken))
            ?? throw new InvalidFileException(ErrorMapping.UnsupportedFileType,
                $"'{fileName}' is not an allowed file or its content does not match the name.");

        var folder = $"applications/{application.Id}";
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

            var attachment = application.AddAttachment(actor, new StoredFile(
                fileName, type.ContentType, originalPath, pdfPath, file.Length), DateTime.UtcNow);

            await _db.SaveChangesAsync(cancellationToken);

            return new AttachmentSummary(attachment.Id, attachment.OriginalFileName,
                attachment.SizeBytes);
        }
        catch
        {
            // Nothing reached the database, so the files on disk would belong to nothing
            if (pdfPath is not null)
                _storage.Delete(pdfPath);
            if (originalPath is not null)
                _storage.Delete(originalPath);
            throw;
        }
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