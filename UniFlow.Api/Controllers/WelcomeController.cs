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

// The start page: what is waiting for the user and what has changed since the previous login,
// by role. Any logged-in user (fallback policy); everything only from what the user may see
// anyway. Lists give their first few and how many there are in all.
[ApiController]
[Route("api/welcome")]
public sealed class WelcomeController : ControllerBase
{
    private const int ListSize = 5;
    private const int NewListSize = 3;
    private const int NewsSize = 3;

    private static readonly ApplicationStatus[] Closed =
        [ApplicationStatus.Approved, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn];

    private readonly UniFlowDbContext _db;
    private readonly ApplicationAccess _access;
    private readonly DraftService _drafts;
    private readonly UserManager<ApplicationUser> _userManager;

    public WelcomeController(UniFlowDbContext db, ApplicationAccess access, DraftService drafts,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _access = access;
        _drafts = drafts;
        _userManager = userManager;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<WelcomeDetails>> Get(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        // At the first login there is nothing "since" - only what is waiting
        var since = user.PreviousLoginAt;

        return new WelcomeDetails(
            user.FirstName,
            since,
            User.IsInRole(RoleNames.Student) ? await StudentAsync(user.Id, since, cancellationToken) : null,
            User.IsInRole(RoleNames.DeanOffice) ? await DeanOfficeAsync(user.Id, cancellationToken) : null,
            User.IsInRole(RoleNames.ViceDean) ? await ViceDeanAsync(cancellationToken) : null,
            User.IsInRole(RoleNames.Administrator) ? await AdminAsync(cancellationToken) : null);
    }

    private async Task<StudentWelcome> StudentAsync(string userId, DateTime? since,
        CancellationToken cancellationToken)
    {
        var mine = _db.Applications.Where(a => a.ApplicantId == userId);

        // One row per application - the latest thing the office did with it, with what it wrote
        // to the student (internal notes stay internal)
        var changes = since is null
            ? []
            : (await mine
                .SelectMany(a => a.StatusHistory
                    .Where(h => h.ChangedAt > since && h.ChangedById != userId)
                    .Select(h => new
                    {
                        a.Id,
                        a.Number,
                        TypeCode = a.ApplicationType.Code,
                        TypeName = a.ApplicationType.Name,
                        h.ToStatus,
                        h.ChangedAt,
                        Comment = h.IsInternal ? null : h.Comment
                    }))
                .ToListAsync(cancellationToken))
                .GroupBy(c => c.Id)
                .Select(g => g.MaxBy(c => c.ChangedAt)!)
                .OrderByDescending(c => c.ChangedAt)
                .Select(c => new WelcomeItem(c.Id, c.Number, c.TypeCode, c.TypeName, null,
                    (ApplicationState)c.ToStatus, c.ChangedAt, c.Comment))
                .ToList();

        // Sent back - with the office's request and when it came, the oldest first. Not
        // UpdatedAt: the student's own new attachment changes it too.
        var toCorrect = (await mine
                .Where(a => a.Status == ApplicationStatus.NeedsCorrection)
                .Select(a => new
                {
                    a.Id,
                    a.Number,
                    TypeCode = a.ApplicationType.Code,
                    TypeName = a.ApplicationType.Name,
                    Request = a.StatusHistory
                        .Where(h => h.ToStatus == ApplicationStatus.NeedsCorrection)
                        .OrderByDescending(h => h.ChangedAt)
                        .Select(h => new { h.ChangedAt, h.Comment })
                        .First()
                })
                .ToListAsync(cancellationToken))
            .OrderBy(a => a.Request.ChangedAt)
            .Select(a => new WelcomeItem(a.Id, a.Number, a.TypeCode, a.TypeName, null,
                ApplicationState.NeedsCorrection, a.Request.ChangedAt, a.Request.Comment))
            .ToList();

        var drafts = await _drafts.ListAsync(userId, cancellationToken);

        return new StudentWelcome(
            Shorten(changes, NewsSize),
            Shorten(toCorrect, ListSize),
            Shorten(drafts, ListSize),
            await mine.CountAsync(a => Application.InProgressStatuses.Contains(a.Status), cancellationToken),
            await mine.CountAsync(a => Closed.Contains(a.Status), cancellationToken));
    }

    private async Task<DeanOfficeWelcome> DeanOfficeAsync(string userId, CancellationToken cancellationToken)
    {
        var visible = _access.VisibleToStaff(User);

        // Corrections back from students first - then the cases taken and not finished, the
        // longest first
        var forYou = await Items(visible
                .Where(a => a.AssignedToId == userId && (a.Status == ApplicationStatus.Resubmitted
                    || a.Status == ApplicationStatus.UnderReview))
                .OrderByDescending(a => a.Status == ApplicationStatus.Resubmitted)
                .ThenBy(a => a.UpdatedAt))
            .ToListAsync(cancellationToken);

        // The list is a queue, oldest first - its front
        var fresh = visible.Where(a => a.Status == ApplicationStatus.Submitted);
        var newToTake = await Items(fresh.OrderBy(a => a.CreatedAt).Take(NewListSize))
            .ToListAsync(cancellationToken);

        return new DeanOfficeWelcome(
            Shorten(forYou, ListSize),
            new WelcomeList<WelcomeItem>(newToTake, await fresh.CountAsync(cancellationToken)),
            await visible.CountAsync(a => a.Status == ApplicationStatus.NeedsCorrection, cancellationToken),
            await visible.CountAsync(a => a.Status == ApplicationStatus.ForwardedToDean, cancellationToken),
            await visible.CountAsync(a => a.AssignedToId != userId && (a.Status == ApplicationStatus.UnderReview
                || a.Status == ApplicationStatus.Resubmitted), cancellationToken));
    }

    // The last change of a forwarded case is its forwarding - At is when it reached the vice-dean
    private async Task<ViceDeanWelcome> ViceDeanAsync(CancellationToken cancellationToken)
    {
        var waiting = _access.VisibleToStaff(User)
            .Where(a => a.Status == ApplicationStatus.ForwardedToDean)
            .OrderBy(a => a.UpdatedAt);

        return new ViceDeanWelcome(new WelcomeList<WelcomeItem>(
            await Items(waiting.Take(ListSize)).ToListAsync(cancellationToken),
            await waiting.CountAsync(cancellationToken)));
    }

    private async Task<AdminWelcome> AdminAsync(CancellationToken cancellationToken)
    {
        var awaitingPassword = _db.Users
            .Where(u => u.IsActive && u.MustChangePassword)
            .OrderBy(u => u.CreatedAt);

        var recent = await _db.AuditLogEntries
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(ListSize)
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

        return new AdminWelcome(
            await _db.Users.CountAsync(u => u.IsActive, cancellationToken),
            await _db.Users.CountAsync(u => !u.IsActive, cancellationToken),
            new WelcomeList<AccountOption>(
                await awaitingPassword.Take(ListSize)
                    .Select(u => new AccountOption(u.Id, u.FirstName + " " + u.LastName))
                    .ToListAsync(cancellationToken),
                await awaitingPassword.CountAsync(cancellationToken)),
            recent
                .Select(r => new AuditLogItem(r.Id, r.CreatedAt, r.ActorName ?? "", (AuditEntryKind)r.Action,
                    r.TargetUserId, r.TargetName, r.Details))
                .ToList());
    }

    // Staff lists: who asked and when it reached the list (submitted or last changed)
    private IQueryable<WelcomeItem> Items(IQueryable<Application> applications) =>
        applications.Select(a => new WelcomeItem(a.Id, a.Number, a.ApplicationType.Code, a.ApplicationType.Name,
            _db.Users.Where(u => u.Id == a.ApplicantId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            (ApplicationState)a.Status,
            a.Status == ApplicationStatus.Submitted ? a.CreatedAt : a.UpdatedAt,
            null));

    private static WelcomeList<T> Shorten<T>(IReadOnlyList<T> all, int size) =>
        new(all.Take(size).ToList(), all.Count);
}