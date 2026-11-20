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

    // draftId: the student's draft whose files become the application's attachments.
    // at: when it happens - now for a student; an earlier moment only for the demo data
    // (DemoDataSeeder), which goes through the same steps.
    // requiredAttachments: how many files the kind needs at least, e.g. 1 for a new last name.
    public async Task<SubmittedApplication> SubmitAsync(string userId, Guid submissionKey,
        int enrollmentId, string typeCode, string formData, Guid? draftId,
        Func<ApplicationHeader, Task<string>> renderHtml, CancellationToken cancellationToken,
        DateTime? at = null, int requiredAttachments = 0)
    {
        // A repeated submission gets the first result and takes no new number
        var alreadySubmitted = await FindSubmittedAsync(userId, submissionKey, cancellationToken);
        if (alreadySubmitted is not null)
            return alreadySubmitted;

        // One reading of the clock: the number, the document and the database must agree
        var now = at ?? DateTime.UtcNow;

        var enrollment = await _db.Enrollments
            .Include(e => e.FieldOfStudy).ThenInclude(f => f.Faculty)
            .SingleOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken)
            ?? throw new DomainException(DomainErrorCode.EnrollmentNotFound, "Enrollment not found.");

        var type = await _db.ApplicationTypes.SingleAsync(t => t.Code == typeCode, cancellationToken);

        // Before taking a number - a rejected submission must not waste one
        Application.EnsureCanBeSubmitted(userId, submissionKey, enrollment, type);


        // One application of a type in progress on given studies
        var inProgress = await _db.Applications
            .Where(a => a.EnrollmentId == enrollmentId && a.ApplicationTypeId == type.Id
                && Application.InProgressStatuses.Contains(a.Status))
            .Select(a => new { a.Id, a.Number, a.SubmissionKey })
            .FirstOrDefaultAsync(cancellationToken);
        if (inProgress is not null)
        {
            // This very submission, saved by a twin request a moment ago - not a second application
            if (inProgress.SubmissionKey == submissionKey)
                return new SubmittedApplication(inProgress.Id, inProgress.Number);

            throw new DomainException(DomainErrorCode.ApplicationAlreadyInProgress,
                $"Application {inProgress.Number} of this type is still in progress on these studies.");
        }

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

        EnsureAttachments(draft?.Files.Count ?? 0, requiredAttachments);
        var number = await _numbers.NextAsync(now, cancellationToken);

        var application = new Application(number, userId, submissionKey, enrollment, type, formData, now);
        var document = await CreateDocumentAsync(application, now, renderHtml, cancellationToken);

        try
        {
            application.AttachGeneratedDocument(document, now);

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
        catch (DbUpdateException exception) when (ViolatedIndex(exception) is not null)
        {
            _storage.Delete(document.StoragePath);

            // Two identical requests at the same moment: the other one has already won.
            // Checked first - the same request breaks both indexes, the database names only one.
            var winner = await FindSubmittedAsync(userId, submissionKey, cancellationToken);
            if (winner is not null)
                return winner;

            // Another application of this type at the same moment, e.g. from a second tab
            if (ViolatedIndex(exception) == ApplicationConfiguration.InProgressIndex)
                throw new DomainException(DomainErrorCode.ApplicationAlreadyInProgress,
                    "Another application of this type was submitted at the same moment.");

            throw;
        }
        catch
        {
            // The database transaction does not cover the disk
            _storage.Delete(document.StoragePath);
            throw;
        }


        return new SubmittedApplication(application.Id, application.Number);
    }


    // The application sent back for correction, submitted again: the files removed and added in
    // the correction draft, the corrected form, a new PDF (the old one stays archived) and the
    // status - in one save. Null when the student has no such application.
    public async Task<SubmittedApplication?> ResubmitAsync(string userId, Guid applicationId,
        string typeCode, string formData, Func<ApplicationHeader, Task<string>> renderHtml,
        CancellationToken cancellationToken, DateTime? at = null, int requiredAttachments = 0)
    {
        var application = await _db.Applications
            .Include(a => a.ApplicationType)
            .Include(a => a.Enrollment).ThenInclude(e => e.FieldOfStudy).ThenInclude(f => f.Faculty)
            .Include(a => a.Attachments)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.ApplicantId == userId,
                cancellationToken);
        if (application is null)
            return null;

        if (application.ApplicationType.Code != typeCode)
            throw new ArgumentException("The application is of another type.", nameof(typeCode));

        var draft = await _db.Drafts
            .Include(d => d.Files)
            .SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);

        var now = at ?? DateTime.UtcNow;
        var applicant = new Actor(userId, ActorRole.Student);

        // Domain first, before the PDF: a wrong status or too many files stop it here.
        // Files are archived before new ones come - the limit counts the active ones.
        if (draft is not null)
        {
            foreach (var attachmentId in draft.RemovedAttachmentIds)
                application.ArchiveAttachment(applicant, attachmentId, now);

            foreach (var file in draft.Files.OrderBy(f => f.UploadedAt))
                application.AddAttachment(applicant, file.ToStoredFile(), now);

            _db.Drafts.Remove(draft);
        }

        // What is left after the correction - the student may have removed the only document
        EnsureAttachments(application.ActiveUploadCount, requiredAttachments);

        application.Resubmit(applicant, formData, now);

        var document = await CreateDocumentAsync(application, now, renderHtml, cancellationToken);

        try
        {
            application.AttachGeneratedDocument(document, now);

            // Files, form, status, the new PDF and the draft: all or nothing
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _storage.Delete(document.StoragePath);
            throw;
        }

        return new SubmittedApplication(application.Id, application.Number);
    }

    // The application's official PDF, saved on disk. The caller attaches it and deletes the file
    // when saving to the database fails.
    private async Task<StoredFile> CreateDocumentAsync(Application application, DateTime now,
        Func<ApplicationHeader, Task<string>> renderHtml, CancellationToken cancellationToken)
    {
        var student = await _db.Users
            .Where(u => u.Id == application.ApplicantId)
            .Select(u => new { u.FirstName, u.LastName })
            .SingleAsync(cancellationToken);

        var albumNumber = await _db.StudentProfiles
            .Where(p => p.UserId == application.ApplicantId)
            .Select(p => p.AlbumNumber)
            .SingleAsync(cancellationToken);

        var header = ApplicationHeaderBuilder.Build(application.Number, now, student.FirstName,
            student.LastName, albumNumber, application.Enrollment);
        var html = await renderHtml(header);
        var pdf = await _pdf.ConvertHtmlToPdfAsync(html, cancellationToken);

        using var content = new MemoryStream(pdf);
        var path = await _storage.SaveAsync(content, $"applications/{application.Id}", ".pdf",
            cancellationToken);

        return new StoredFile($"Wniosek_{application.Number.Replace('/', '-')}.pdf",
            Attachment.PdfContentType, path, null, pdf.Length);
    }

    // Before a number and a PDF - a refused submission must not waste them
    private static void EnsureAttachments(int count, int required)
    {
        if (count < required)
            throw new DomainException(DomainErrorCode.AttachmentRequired,
                $"This application needs at least {required} attachment(s).");
    }

    private Task<SubmittedApplication?> FindSubmittedAsync(string userId, Guid submissionKey,
    CancellationToken cancellationToken) =>
    _db.Applications
        .Where(a => a.ApplicantId == userId && a.SubmissionKey == submissionKey)
        .Select(a => new SubmittedApplication(a.Id, a.Number))
        .SingleOrDefaultAsync(cancellationToken);

    // The name of the unique index that stopped the save, or null for any other error
    private static string? ViolatedIndex(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
            && postgres.ConstraintName is ApplicationConfiguration.SubmissionKeyIndex
                or ApplicationConfiguration.InProgressIndex
            ? postgres.ConstraintName
            : null;
}