using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Texts;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;
using UniFlow.Shared.Applications;
using UniFlow.Shared.Enrollments;
using UniFlow.Shared.Review;

namespace UniFlow.Api.Applications;

// Builds the application page. The student sees their case; an employee also sees
// internal notes, removed files and older versions of the PDF (nothing is ever deleted).
public sealed class ApplicationDetailsQuery
{
    private readonly UniFlowDbContext _db;
    private readonly ApplicationAccess _access;

    public ApplicationDetailsQuery(UniFlowDbContext db, ApplicationAccess access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApplicationDetails> GetAsync(Guid applicationId, ApplicationViewer viewer,
        ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var staff = viewer == ApplicationViewer.Staff;

        var application = await _db.Applications
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.ApplicationType)
            .Include(a => a.Enrollment).ThenInclude(e => e.FieldOfStudy).ThenInclude(f => f.Faculty)
            .Include(a => a.StatusHistory)
            .Include(a => a.Attachments)
            .SingleAsync(a => a.Id == applicationId, cancellationToken);

        // Everyone named on the page, in one query
        var userIds = application.StatusHistory.Select(h => h.ChangedById)
            .Append(application.ApplicantId)
            .Append(application.AssignedToId)
            .OfType<string>()
            .Distinct()
            .ToList();

        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, cancellationToken);

        var albumNumber = await _db.StudentProfiles
            .Where(p => p.UserId == application.ApplicantId)
            .Select(p => p.AlbumNumber)
            .SingleOrDefaultAsync(cancellationToken);

        var enrollment = application.Enrollment;
        var studies = new EnrollmentSummary(enrollment.Id, enrollment.FieldOfStudy.Faculty.Name,
            enrollment.FieldOfStudy.Name, StudyTexts.Level(enrollment.FieldOfStudy.Level),
            StudyTexts.Mode(enrollment.StudyMode), enrollment.Semester);

        var attachments = application.Attachments
            .Where(at => staff || !at.IsArchived)
            .OrderBy(at => at.Kind != AttachmentKind.GeneratedDocument)
            .ThenBy(at => at.UploadedAt)
            .Select(at => new AttachmentInfo(at.Id, at.OriginalFileName, at.SizeBytes,
                at.Kind == AttachmentKind.GeneratedDocument, at.UploadedAt, at.ArchivedAt))
            .ToList();

        var history = application.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new StatusChange(
                (ApplicationState?)h.FromStatus,
                (ApplicationState)h.ToStatus,
                staff ? names.GetValueOrDefault(h.ChangedById, "") : ChangedByForStudent(h, application),
                h.ChangedAt,
                // The student sees that the case moved on, but not the staff's internal note
                h.IsInternal && !staff ? null : h.Comment,
                h.IsInternal && staff))
            .ToList();

        var actions = staff
            ? ReviewRules.ActionsFor(application.Status, application.RequiresDeanDecision,
                user.IsInRole(RoleNames.DeanOffice), user.IsInRole(RoleNames.ViceDean))
            : [];

        // For staff: the university e-mail to write to the student, and the student's other
        // applications - through the same filter as the list, so nothing beyond what the
        // employee could open anyway (a vice-dean sees only the forwarded ones)
        var studentEmail = staff
            ? await _db.Users
                .Where(u => u.Id == application.ApplicantId)
                .Select(u => u.Email)
                .SingleAsync(cancellationToken)
            : null;

        var otherApplications = staff
            ? await _access.VisibleToStaff(user)
                .Where(a => a.ApplicantId == application.ApplicantId && a.Id != application.Id)
                .OrderByDescending(a => a.CreatedAt)
                                .Select(a => new { a.Id, a.Number, TypeName = a.ApplicationType.Name, a.Status, a.CreatedAt })
                .ToListAsync(cancellationToken)
            : [];

        var related = otherApplications
            .Select(a => new RelatedApplication(a.Id, a.Number, a.TypeName, (ApplicationState)a.Status,
                Application.InProgressStatuses.Contains(a.Status), a.CreatedAt))
            .ToList();

        return new ApplicationDetails(
            application.Id,
            application.Number,
            application.ApplicationType.Code,
            application.ApplicationType.Name,
            (ApplicationState)application.Status,
            application.CreatedAt,
            application.RequiresDeanDecision,
            names.GetValueOrDefault(application.ApplicantId, ""),
            albumNumber ?? "",
            studies,
            // Who handles the case is staff information - the student is not given names
            staff && application.AssignedToId is not null
                ? names.GetValueOrDefault(application.AssignedToId)
                : null,
            application.FormData,
            attachments,
            history,
            actions,
            ForApplicant: !staff,
            CanWithdraw: !staff && ReviewRules.CanWithdraw(application.Status),
            StudentEmail: studentEmail,
            OtherApplications: related,
            MaxAttachments: application.MaxAttachments);
    }

    // The student is told which office acted, not which employee - as on paper
    private static string ChangedByForStudent(StatusHistoryEntry entry, Application application) =>
        entry.ChangedById == application.ApplicantId ? "Ty"
        : entry.FromStatus == ApplicationStatus.ForwardedToDean ? "Prodziekan"
        : "Dziekanat";
}