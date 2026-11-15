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

    // draftId: the student's draft whose files become the application's attachments
    public async Task<SubmittedApplication> SubmitAsync(string userId, Guid submissionKey,
        int enrollmentId, string typeCode, string formData, Guid? draftId,
        Func<ApplicationHeader, Task<string>> renderHtml, CancellationToken cancellationToken)
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

        Draft? draft = null;
        if (draftId is not null)
        {
            draft = await _db.Drafts
                .Include(d => d.Files)
                .SingleOrDefaultAsync(d => d.Id == draftId && d.StudentId == userId, cancellationToken)
                ?? throw new DomainException(DomainErrorCode.DraftNotFound, "Draft not found.");

            if (draft.ApplicationTypeId != type.Id)
                throw new ArgumentException("The draft is of another application type.", nameof(draftId));
        }

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

            // The draft's files become the application's attachments - the same files on disk -
            // and the draft is gone
            if (draft is not null)
            {
                var applicant = new Actor(userId, ActorRole.Student);
                foreach (var file in draft.Files.OrderBy(f => f.UploadedAt))
                    application.AddAttachment(applicant, file.ToStoredFile(), now);

                _db.Drafts.Remove(draft);
            }

            _db.Applications.Add(application);

            // Application, status history, attachments and the draft: all or nothing
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