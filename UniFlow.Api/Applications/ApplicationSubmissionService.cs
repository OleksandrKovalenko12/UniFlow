using Microsoft.EntityFrameworkCore;
using UniFlow.Documents.Models;
using UniFlow.Domain;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Numbering;
using UniFlow.Infrastructure.Pdf;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Infrastructure.Storage;
using UniFlow.Shared.Applications;
using Npgsql;
using UniFlow.Infrastructure.Persistence.Configurations;

namespace UniFlow.Api.Applications;

// The submission steps shared by all application types; knows nothing about HTTP
public sealed class ApplicationSubmissionService
{
    private readonly UniFlowDbContext _db;
    private readonly IApplicationNumberGenerator _numbers;
    private readonly IPdfConverter _pdf;
    private readonly IFileStorage _storage;

    public ApplicationSubmissionService(UniFlowDbContext db, IApplicationNumberGenerator numbers,
        IPdfConverter pdf, IFileStorage storage)
    {
        _db = db;
        _numbers = numbers;
        _pdf = pdf;
        _storage = storage;
    }

    public async Task<SubmittedApplication> SubmitAsync(string userId, Guid submissionKey, 
        int enrollmentId, string typeCode, string formData, Func<ApplicationHeader, Task<string>> renderHtml,
        CancellationToken cancellationToken)
    {
        // A repeated submission gets the first result and takes no new number
        var alreadySubmitted = await FindSubmittedAsync(userId, submissionKey, cancellationToken);
        if (alreadySubmitted is not null)
            return alreadySubmitted;

        // One reading of the clock: the number, the document and the database must agree
        var now = DateTime.UtcNow;

        var enrollment = await _db.Enrollments
            .Include(e => e.FieldOfStudy).ThenInclude(f => f.Faculty)
            .SingleOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken)
            ?? throw new DomainException(DomainErrorCode.EnrollmentNotFound, "Enrollment not found.");

        var type = await _db.ApplicationTypes.SingleAsync(t => t.Code == typeCode, cancellationToken);

        // Before taking a number - a rejected submission must not waste one
        Application.EnsureCanBeSubmitted(userId, submissionKey, enrollment, type);

        var student = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName })
            .SingleAsync(cancellationToken);

        var albumNumber = await _db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.AlbumNumber)
            .SingleAsync(cancellationToken);

        var number = await _numbers.NextAsync(now, cancellationToken);

        var application = new Application(number, userId, submissionKey, enrollment, type, formData, now);

        var header = ApplicationHeaderBuilder.Build(number, now, student.FirstName, student.LastName,
            albumNumber, enrollment);
        var html = await renderHtml(header);
        var pdf = await _pdf.ConvertHtmlToPdfAsync(html, cancellationToken);

        using var content = new MemoryStream(pdf);
        var path = await _storage.SaveAsync(content, $"applications/{application.Id}", ".pdf",
            cancellationToken);

        try
        {
            application.AttachGeneratedDocument(new StoredFile(
                $"Wniosek_{number.Replace('/', '-')}.pdf", Attachment.PdfContentType, path, null,
                pdf.Length), now);

            _db.Applications.Add(application);

            // Application, status history and attachment: all or nothing
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateSubmission(exception))
        {
            // Two identical requests at the same moment: the other one has already won
            _storage.Delete(path);

            return await FindSubmittedAsync(userId, submissionKey, cancellationToken)
                ?? throw new InvalidOperationException("The winning submission was not found.");
        }
        catch
        {
            // The database transaction does not cover the disk
            _storage.Delete(path);
            throw;
        }


        return new SubmittedApplication(application.Id, application.Number);
    }

    private Task<SubmittedApplication?> FindSubmittedAsync(string userId, Guid submissionKey,
    CancellationToken cancellationToken) =>
    _db.Applications
        .Where(a => a.ApplicantId == userId && a.SubmissionKey == submissionKey)
        .Select(a => new SubmittedApplication(a.Id, a.Number))
        .SingleOrDefaultAsync(cancellationToken);

    private static bool IsDuplicateSubmission(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ApplicationConfiguration.SubmissionKeyIndex
        };
}