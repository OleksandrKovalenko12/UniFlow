using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Applications;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;
using UniFlow.Shared.Admin;
using UniFlow.Shared.Applications;
using UniFlow.Shared.Welcome;

namespace UniFlow.Api.Controllers;

// The start page: what has changed since the previous login and what is waiting, by role.
// Any logged-in user (fallback policy); every number only from what the user may see anyway.
[ApiController]
[Route("api/welcome")]
public sealed class WelcomeController : ControllerBase
{
    private const int MaxChanges = 10;
    private const int ShortList = 3;
    private const int RecentAuditEntries = 5;

    private readonly UniFlowDbContext _db;
    private readonly ApplicationAccess _access;
    private readonly UserManager<ApplicationUser> _userManager;

    public WelcomeController(UniFlowDbContext db, ApplicationAccess access,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _access = access;
        _userManager = userManager;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<WelcomeDetails>> Get(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        // At the first login there is nothing "since" - only the current state
        var since = user.PreviousLoginAt;

        return new WelcomeDetails(
            user.FirstName,
            since,
            User.IsInRole(RoleNames.Student) ? await StudentAsync(user.Id, since, cancellationToken) : null,
            User.IsInRole(RoleNames.DeanOffice) ? await DeanOfficeAsync(user.Id, since, cancellationToken) : null,
            User.IsInRole(RoleNames.ViceDean) ? await ViceDeanAsync(since, cancellationToken) : null,
            User.IsInRole(RoleNames.Administrator) ? await AdminAsync(cancellationToken) : null);
    }

    private async Task<StudentWelcome> StudentAsync(string userId, DateTime? since,
        CancellationToken cancellationToken)
    {
        var mine = _db.Applications.Where(a => a.ApplicantId == userId);

        var inProgress = await mine.CountAsync(a => Application.InProgressStatuses.Contains(a.Status),
            cancellationToken);
        var needsCorrection = await mine.CountAsync(a => a.Status == ApplicationStatus.NeedsCorrection,
            cancellationToken);
        // Corrections of sent-back applications are not drafts of new ones
        var drafts = await _db.Drafts.CountAsync(d => d.StudentId == userId && d.ApplicationId == null,
            cancellationToken);

        var changes = since is null
            ? []
            : await mine
                .SelectMany(a => a.StatusHistory
                    .Where(h => h.ChangedAt > since && h.ChangedById != userId)
                    .Select(h => new { a.Id, a.Number, TypeName = a.ApplicationType.Name, h.ToStatus, h.ChangedAt }))
                .ToListAsync(cancellationToken);

        // Where things stand, for a quiet day - the latest applications as they are now
        var recent = await mine
            .OrderByDescending(a => a.CreatedAt)
            .Take(ShortList)
            .Select(a => new { a.Id, a.Number, TypeName = a.ApplicationType.Name, a.Status, a.CreatedAt })
            .ToListAsync(cancellationToken);

        // One row per application - the latest thing the office did with it
        return new StudentWelcome(inProgress, needsCorrection, drafts,
            changes
                .GroupBy(c => c.Id)
                .Select(g => g.MaxBy(c => c.ChangedAt)!)
                .OrderByDescending(c => c.ChangedAt)
                .Take(MaxChanges)
                .Select(c => new WelcomeChange(c.Id, c.Number, c.TypeName, (ApplicationState)c.ToStatus, c.ChangedAt))
                .ToList(),
            recent
                .Select(a => new WelcomeChange(a.Id, a.Number, a.TypeName, (ApplicationState)a.Status, a.CreatedAt))
                .ToList());
    }

    private async Task<DeanOfficeWelcome> DeanOfficeAsync(string userId, DateTime? since,
        CancellationToken cancellationToken)
    {
        var visible = _access.VisibleToStaff(User);

        // The student's correction came back to the employee who sent it back
        var myCorrected = await visible
            .Where(a => a.Status == ApplicationStatus.Resubmitted && a.AssignedToId == userId)
            .OrderBy(a => a.UpdatedAt)
            .Select(a => new { a.Id, a.Number, TypeName = a.ApplicationType.Name, a.Status, a.UpdatedAt })
            .ToListAsync(cancellationToken);

        var myInReview = await visible.CountAsync(a => a.Status == ApplicationStatus.UnderReview
            && a.AssignedToId == userId, cancellationToken);

        // The list is a queue, oldest first - its front
        var oldestNew = await visible
            .Where(a => a.Status == ApplicationStatus.Submitted)
            .OrderBy(a => a.CreatedAt)
            .Take(ShortList)
            .Select(a => new { a.Id, a.Number, TypeName = a.ApplicationType.Name, a.Status, a.CreatedAt })
            .ToListAsync(cancellationToken);

        var fresh = await visible.CountAsync(a => a.Status == ApplicationStatus.Submitted, cancellationToken);
        var freshSince = since is null ? 0
            : await visible.CountAsync(a => a.Status == ApplicationStatus.Submitted && a.CreatedAt > since,
                cancellationToken);
        var othersCorrected = await visible.CountAsync(a => a.Status == ApplicationStatus.Resubmitted
            && a.AssignedToId != userId, cancellationToken);
        var waiting = await visible.CountAsync(a => a.Status == ApplicationStatus.NeedsCorrection,
            cancellationToken);

        return new DeanOfficeWelcome(
            myCorrected
                .Select(a => new WelcomeChange(a.Id, a.Number, a.TypeName, (ApplicationState)a.Status, a.UpdatedAt))
                .ToList(),
            myInReview,
            oldestNew
                .Select(a => new WelcomeChange(a.Id, a.Number, a.TypeName, (ApplicationState)a.Status, a.CreatedAt))
                .ToList(),
            fresh, freshSince, othersCorrected, waiting);
    }

    // Forwarded and not decided yet - the last change of such a case is the forwarding
    private async Task<ViceDeanWelcome> ViceDeanAsync(DateTime? since, CancellationToken cancellationToken)
    {
        var forwarded = _access.VisibleToStaff(User).Where(a => a.Status == ApplicationStatus.ForwardedToDean);

        return new ViceDeanWelcome(
            await forwarded.CountAsync(cancellationToken),
            since is null ? 0 : await forwarded.CountAsync(a => a.UpdatedAt > since, cancellationToken));
    }

    private async Task<AdminWelcome> AdminAsync(CancellationToken cancellationToken)
    {
        var active = await _db.Users.CountAsync(u => u.IsActive, cancellationToken);
        var inactive = await _db.Users.CountAsync(u => !u.IsActive, cancellationToken);
        var awaitingPassword = await _db.Users.CountAsync(u => u.IsActive && u.MustChangePassword,
            cancellationToken);

        var recent = await _db.AuditLogEntries
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(RecentAuditEntries)
            .Select(e => new
            {
                e.Id,
                e.CreatedAt,
                ActorName = _db.Users.Where(u => u.Id == e.ActorId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                e.Action,
                e.TargetUserId,
                TargetName = _db.Users.Where(u => u.Id == e.TargetUserId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                e.Details
            })
            .ToListAsync(cancellationToken);

        return new AdminWelcome(active, inactive, awaitingPassword, recent
            .Select(r => new AuditLogItem(r.Id, r.CreatedAt, r.ActorName ?? "", (AuditEntryKind)r.Action,
                r.TargetUserId, r.TargetName, r.Details))
            .ToList());
    }
}