using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Texts;
using UniFlow.Domain;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared.Drafts;

namespace UniFlow.Api.Applications;

// The student's unfinished applications. Everything here works only on the student's own drafts:
// someone else's draft looks like a missing one.
public sealed class DraftService
{
    private readonly UniFlowDbContext _db;
    private readonly FileIntake _intake;

    public DraftService(UniFlowDbContext db, FileIntake intake)
    {
        _db = db;
        _intake = intake;
    }

    public async Task<IReadOnlyList<DraftSummary>> ListAsync(string userId,
        CancellationToken cancellationToken)
    {
        var drafts = await _db.Drafts
            .AsNoTracking()
            .Include(d => d.ApplicationType)
            .Include(d => d.Enrollment).ThenInclude(e => e.FieldOfStudy)
            .Include(d => d.Files)
            // Corrections belong to their application's card, not to "Niedokończone"
            .Where(d => d.StudentId == userId && d.ApplicationId == null)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(cancellationToken);

        return drafts
            .Select(d => new DraftSummary(d.Id, d.ApplicationType.Code, d.ApplicationType.Name,
                d.EnrollmentId, d.Enrollment.FieldOfStudy.Name,
                StudyTexts.Level(d.Enrollment.FieldOfStudy.Level), d.UpdatedAt, d.ExpiresAt,
                d.Files.Count))
            .ToList();
    }

    public async Task<DraftDetails?> GetAsync(string userId, string typeCode,
        CancellationToken cancellationToken)
    {
        var draft = await FindByTypeAsync(userId, typeCode, cancellationToken);
        return draft is null ? null : ToDetails(draft);
    }

    // Creates the draft on the first save, then keeps updating the same one
    public async Task<DraftDetails> SaveAsync(string userId, string typeCode,
        SaveDraftRequest request, CancellationToken cancellationToken)
    {
        EnsureJson(request.FormData);

        var enrollment = await _db.Enrollments
            .SingleOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new DomainException(DomainErrorCode.EnrollmentNotFound, "Enrollment not found.");

        var now = DateTime.UtcNow;
        var draft = await FindByTypeAsync(userId, typeCode, cancellationToken);

        if (draft is null)
        {
            var type = await _db.ApplicationTypes.SingleOrDefaultAsync(t => t.Code == typeCode,
                    cancellationToken)
                ?? throw new DomainException(DomainErrorCode.ApplicationTypeInactive,
                    $"Unknown application type '{typeCode}'.");

            draft = new Draft(userId, enrollment, type, request.FormData, now);
            _db.Drafts.Add(draft);
        }
        else
        {
            draft.Update(enrollment, request.FormData, now);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToDetails(draft);
    }

    // The changes to the student's application sent back for correction; null when there are none
    public async Task<DraftDetails?> GetCorrectionAsync(string userId, Guid applicationId,
        CancellationToken cancellationToken)
    {
        var draft = await _db.Drafts
            .Include(d => d.ApplicationType)
            .Include(d => d.Files)
            .SingleOrDefaultAsync(d => d.ApplicationId == applicationId && d.StudentId == userId,
                cancellationToken);

        return draft is null ? null : ToDetails(draft);
    }

    // Creates the correction draft on the first save, then keeps updating it.
    // Null when the student has no such application.
    public async Task<DraftDetails?> SaveCorrectionAsync(string userId, Guid applicationId,
        SaveCorrectionRequest request, CancellationToken cancellationToken)
    {
        EnsureJson(request.FormData);

        var application = await _db.Applications
            .Include(a => a.Enrollment)
            .Include(a => a.ApplicationType)
            .Include(a => a.Attachments)
            .SingleOrDefaultAsync(a => a.Id == applicationId && a.ApplicantId == userId,
                cancellationToken);
        if (application is null)
            return null;

        var now = DateTime.UtcNow;
        var draft = await _db.Drafts
            .Include(d => d.Files)
            .SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);

        if (draft is null)
        {
            draft = Draft.ForCorrection(application, request.FormData, now);
            _db.Drafts.Add(draft);
        }

        // The application is already loaded, so the draft sees its files
        draft.UpdateCorrection(request.FormData, request.RemovedAttachmentIds, now);

        await _db.SaveChangesAsync(cancellationToken);
        return ToDetails(draft);
    }

    // The application was withdrawn: its unsent changes and their files go too
    public async Task DeleteCorrectionAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var draft = await _db.Drafts
            .Include(d => d.Files)
            .SingleOrDefaultAsync(d => d.ApplicationId == applicationId, cancellationToken);

        if (draft is not null)
            await DeleteDraftsAsync([draft], cancellationToken);
    }

    // Null when there is no such draft of this student
    public async Task<DraftFileInfo?> AddFileAsync(string userId, Guid draftId, IFormFile file,
        CancellationToken cancellationToken)
    {
        var draft = await FindAsync(userId, draftId, cancellationToken);
        if (draft is null)
            return null;

        // The limit before the file is saved and converted - that takes seconds
        draft.EnsureCanAddFile();

        var stored = await _intake.StoreAsync(file, $"uploads/{draft.Id}", cancellationToken);

        try
        {
            var added = draft.AddFile(stored, DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);

            return new DraftFileInfo(added.Id, added.OriginalFileName, added.SizeBytes);
        }
        catch
        {
            // Nothing reached the database, so the file on disk would belong to nothing
            _intake.Delete(stored);
            throw;
        }
    }

    // False when there is no such draft of this student
    public async Task<bool> RemoveFileAsync(string userId, Guid draftId, Guid fileId,
        CancellationToken cancellationToken)
    {
        var draft = await FindAsync(userId, draftId, cancellationToken);
        if (draft is null)
            return false;

        var removed = draft.RemoveFile(fileId, DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        // Only after the database: a failed save must not leave a row without its file
        _intake.Delete(removed.ToStoredFile());
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, Guid draftId,
        CancellationToken cancellationToken)
    {
        var draft = await FindAsync(userId, draftId, cancellationToken);
        if (draft is null)
            return false;

        await DeleteDraftsAsync([draft], cancellationToken);
        return true;
    }

    // The file shown in the preview: always its PDF version
    public async Task<(string Path, string FileName)?> FindFileAsync(string userId, Guid draftId,
        Guid fileId, CancellationToken cancellationToken)
    {
        var file = await _db.Drafts
            .Where(d => d.Id == draftId && d.StudentId == userId)
            .SelectMany(d => d.Files)
            .Where(f => f.Id == fileId)
            .Select(f => new { Path = f.PdfStoragePath ?? f.StoragePath, f.OriginalFileName })
            .SingleOrDefaultAsync(cancellationToken);

        return file is null ? null : (file.Path, Path.ChangeExtension(file.OriginalFileName, ".pdf"));
    }

    // Drafts nobody has touched for Draft.RetentionDays days go, with their files
    public async Task<int> DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken)
    {
        var oldest = now.AddDays(-Draft.RetentionDays);

        var expired = await _db.Drafts
            .Include(d => d.Files)
            .Where(d => d.UpdatedAt < oldest)
            .ToListAsync(cancellationToken);

        await DeleteDraftsAsync(expired, cancellationToken);
        return expired.Count;
    }

    private async Task DeleteDraftsAsync(IReadOnlyList<Draft> drafts,
        CancellationToken cancellationToken)
    {
        var files = drafts.SelectMany(d => d.Files).Select(f => f.ToStoredFile()).ToList();

        _db.Drafts.RemoveRange(drafts);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var file in files)
            _intake.Delete(file);
    }

    // Any form may be unfinished, but it must be JSON - the column is jsonb
    private static void EnsureJson(string formData)
    {
        try
        {
            using var _ = JsonDocument.Parse(formData);
        }
        catch (JsonException)
        {
            throw new ArgumentException("Form data is not valid JSON.", nameof(formData));
        }
    }

    private Task<Draft?> FindByTypeAsync(string userId, string typeCode,
        CancellationToken cancellationToken) =>
        _db.Drafts
            .Include(d => d.ApplicationType)
            .Include(d => d.Files)
            .SingleOrDefaultAsync(d => d.StudentId == userId && d.ApplicationId == null
                && d.ApplicationType.Code == typeCode, cancellationToken);

    // A correction draft brings its application's files - the file limit depends on them
    private Task<Draft?> FindAsync(string userId, Guid draftId,
        CancellationToken cancellationToken) =>
        _db.Drafts
            .Include(d => d.ApplicationType)
            .Include(d => d.Files)
            .Include(d => d.Application).ThenInclude(a => a!.Attachments)
            .SingleOrDefaultAsync(d => d.Id == draftId && d.StudentId == userId, cancellationToken);

    private static DraftDetails ToDetails(Draft draft) =>
        new(draft.Id, draft.ApplicationType.Code, draft.EnrollmentId, draft.FormData,
            draft.UpdatedAt, draft.ExpiresAt,
            draft.Files.OrderBy(f => f.UploadedAt)
                .Select(f => new DraftFileInfo(f.Id, f.OriginalFileName, f.SizeBytes))
                .ToList(),
            draft.RemovedAttachmentIds);
}