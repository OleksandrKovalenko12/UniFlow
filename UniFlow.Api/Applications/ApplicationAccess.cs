using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;

namespace UniFlow.Api.Applications;

public enum ApplicationViewer
{
    Applicant,
    Staff
}

// Who may open an application: its author, or an employee assigned to its faculty.
// The role says WHAT someone may do, the assignment says WHERE (docs/model-domeny.md).
public sealed class ApplicationAccess
{
    private readonly UniFlowDbContext _db;

    public ApplicationAccess(UniFlowDbContext db)
    {
        _db = db;
    }

    public static bool IsStaff(ClaimsPrincipal user) =>
        user.IsInRole(RoleNames.DeanOffice) || user.IsInRole(RoleNames.ViceDean);

    // Applications of the faculties the employee works for - never their own,
    // because they could not decide on them anyway (conflict of interest)
    public IQueryable<Application> VisibleToStaff(string userId) =>
        _db.Applications.Where(a => a.ApplicantId != userId
            && _db.StaffAssignments.Any(s => s.UserId == userId
                && s.FacultyId == a.Enrollment.FieldOfStudy.FacultyId));

    // Null when the application does not exist or the user may not see it -
    // both end as the same 404, so nobody learns which numbers exist
    public async Task<ApplicationViewer?> GetViewerAsync(Guid applicationId, ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var applicantId = await _db.Applications
            .Where(a => a.Id == applicationId)
            .Select(a => a.ApplicantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (applicantId is null)
            return null;

        // Their own application: always the student's view, also for a student who works
        // in the dean's office
        if (applicantId == userId)
            return ApplicationViewer.Applicant;

        if (IsStaff(user) && await VisibleToStaff(userId)
                .AnyAsync(a => a.Id == applicationId, cancellationToken))
            return ApplicationViewer.Staff;

        return null;
    }
}