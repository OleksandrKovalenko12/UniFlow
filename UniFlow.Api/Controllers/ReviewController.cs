using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Applications;
using UniFlow.Api.Texts;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;
using UniFlow.Shared.Applications;
using UniFlow.Shared.Review;
using UniFlow.Domain;
using UniFlow.Domain.Entities;

namespace UniFlow.Api.Controllers;

// The dean's office and the vice-dean: applications of their own faculties
[ApiController]
[Route("api/review")]
[Authorize(Roles = RoleNames.DeanOffice + "," + RoleNames.ViceDean)]
public sealed class ReviewController : ControllerBase
{
    public const int PageSize = 25;

    private readonly UniFlowDbContext _db;
    private readonly ApplicationAccess _access;
    private readonly ApplicationDetailsQuery _details;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReviewController(UniFlowDbContext db, ApplicationAccess access,
        ApplicationDetailsQuery details, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _access = access;
        _details = details;
        _userManager = userManager;
    }

    // corrected: in the "Nowe" tab, true = only corrected applications (Złożony ponownie),
    // false = only new ones (Złożony). assigned: AssignedFilter.Me, AssignedFilter.None or an
    // employee's id.
    [HttpGet]
    public async Task<PagedList<ReviewListItem>> GetList(ReviewTab tab = ReviewTab.New,
        string? search = null, string? type = null, bool? corrected = null,
        string? assigned = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User)!;
        var statuses = ReviewRules.StatusesOf(tab);
        var query = _access.VisibleToStaff(User).Where(a => statuses.Contains(a.Status));

        if (corrected is not null)
            query = query.Where(a => (a.Status == ApplicationStatus.Resubmitted) == corrected);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(a => a.ApplicationType.Code == type);

        query = assigned switch
        {
            null or "" => query,
            AssignedFilter.Me => query.Where(a => a.AssignedToId == userId),
            AssignedFilter.None => query.Where(a => a.AssignedToId == null),
            _ => query.Where(a => a.AssignedToId == assigned)
        };

        // Part of the student's name ("kowal", "jan ziel"), of the album number
        // or of the application number ("0012", "UF/2026/0012")
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Number.ToLower().Contains(term)
                || _db.Users.Any(u => u.Id == a.ApplicantId
                    && (u.FirstName + " " + u.LastName).ToLower().Contains(term))
                || _db.StudentProfiles.Any(p => p.UserId == a.ApplicantId
                    && p.AlbumNumber.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // A queue: the oldest first, corrected applications before new ones.
        // Closed cases and the whole list are looked up, not worked through - the latest first.
        query = tab switch
        {
            ReviewTab.New => query
                .OrderByDescending(a => a.Status == ApplicationStatus.Resubmitted)
                .ThenBy(a => a.CreatedAt),
            ReviewTab.Closed or ReviewTab.All => query.OrderByDescending(a => a.UpdatedAt),
            _ => query.OrderBy(a => a.CreatedAt)
        };

        page = Math.Max(page, 1);

        var rows = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(a => new
            {
                a.Id,
                a.Number,
                TypeName = a.ApplicationType.Name,
                StudentName = _db.Users.Where(u => u.Id == a.ApplicantId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                AlbumNumber = _db.StudentProfiles.Where(p => p.UserId == a.ApplicantId)
                    .Select(p => p.AlbumNumber).FirstOrDefault(),
                FieldOfStudyName = a.Enrollment.FieldOfStudy.Name,
                a.Enrollment.FieldOfStudy.Level,
                FacultyCode = a.Enrollment.FieldOfStudy.Faculty.Code,
                a.Status,
                a.CreatedAt,
                AssignedToName = _db.Users.Where(u => u.Id == a.AssignedToId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                AssignedToMe = a.AssignedToId == userId
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ReviewListItem(r.Id, r.Number, r.TypeName, r.StudentName ?? "",
                r.AlbumNumber ?? "", r.FieldOfStudyName, StudyTexts.Level(r.Level), r.FacultyCode,
                (ApplicationState)r.Status, r.CreatedAt, r.AssignedToName, r.AssignedToMe))
            .ToList();

        return new PagedList<ReviewListItem>(items, totalCount, page, PageSize);
    }

    // Decisions: one endpoint per Domain method. Each returns the application as it is now,
    // so the page shows the new status, history and buttons without asking again.

    [HttpPost("/api/applications/{id:guid}/take")]
    public Task<ActionResult<ApplicationDetails>> Take(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(id, (application, actor, now) => application.TakeForReview(actor, now),
            cancellationToken);

    [HttpPost("/api/applications/{id:guid}/request-correction")]
    public Task<ActionResult<ApplicationDetails>> RequestCorrection(Guid id, CommentRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(id, (application, actor, now) =>
            application.RequestCorrection(actor, request.Comment, now), cancellationToken);

    [HttpPost("/api/applications/{id:guid}/forward")]
    public Task<ActionResult<ApplicationDetails>> Forward(Guid id, NoteRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(id, (application, actor, now) =>
            application.ForwardToDean(actor, request.Comment, now), cancellationToken);

    [HttpPost("/api/applications/{id:guid}/approve")]
    public Task<ActionResult<ApplicationDetails>> Approve(Guid id, CancellationToken cancellationToken) =>
        DecideAsync(id, (application, actor, now) => application.Approve(actor, now),
            cancellationToken);

    [HttpPost("/api/applications/{id:guid}/reject")]
    public Task<ActionResult<ApplicationDetails>> Reject(Guid id, CommentRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(id, (application, actor, now) =>
            application.Reject(actor, request.Comment, now), cancellationToken);

    [HttpPost("/api/applications/{id:guid}/return")]
    public Task<ActionResult<ApplicationDetails>> Return(Guid id, CommentRequest request,
        CancellationToken cancellationToken) =>
        DecideAsync(id, (application, actor, now) =>
            application.ReturnToDeanOffice(actor, request.Comment, now), cancellationToken);

    // Found only among the employee's own faculties (never their own application), decided by
    // Domain, saved with the row version check - two people clicking at once get 409
    private async Task<ActionResult<ApplicationDetails>> DecideAsync(Guid id,
        Action<Application, Actor, DateTime> decide, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User)!;

        var application = await _access.VisibleToStaff(User)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (application is null)
            return NotFound();

        // A case forwarded to the vice-dean is theirs; everything before belongs to the
        // dean's office. The user must really have that role - Domain checks the rest.
        var role = application.Status == ApplicationStatus.ForwardedToDean
            ? ActorRole.ViceDean
            : ActorRole.DeanOffice;

        if (!User.IsInRole(role == ActorRole.ViceDean ? RoleNames.ViceDean : RoleNames.DeanOffice))
            throw new DomainException(DomainErrorCode.RoleNotAllowed,
                $"Only {role} can decide on this application now.");

        decide(application, new Actor(userId, role), DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return await _details.GetAsync(id, ApplicationViewer.Staff, User, cancellationToken);
    }

    // Colleagues from the dean's office of my faculties, for the "Prowadzi" filter (me excluded -
    // "Ja" covers that)
    [HttpGet("staff")]
    public async Task<IReadOnlyList<StaffMember>> GetStaff(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User)!;

        var myFaculties = _db.StaffAssignments.Where(s => s.UserId == userId).Select(s => s.FacultyId);
        var deanOfficeRole = _db.Roles.Where(r => r.Name == RoleNames.DeanOffice).Select(r => r.Id);

        return await _db.Users
            .Where(u => u.Id != userId
                && _db.StaffAssignments.Any(s => s.UserId == u.Id && myFaculties.Contains(s.FacultyId))
                && _db.UserRoles.Any(ur => ur.UserId == u.Id && deanOfficeRole.Contains(ur.RoleId)))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new StaffMember(u.Id, u.FirstName + " " + u.LastName))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("counts")]
    public async Task<ReviewCounts> GetCounts(CancellationToken cancellationToken)
    {
        var byStatus = await _access.VisibleToStaff(User)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        int CountOf(ReviewTab tab) =>
            ReviewRules.StatusesOf(tab).Sum(status => byStatus.GetValueOrDefault(status));

        return new ReviewCounts(CountOf(ReviewTab.New), CountOf(ReviewTab.InReview),
            CountOf(ReviewTab.WaitingForStudent), CountOf(ReviewTab.AtViceDean),
            CountOf(ReviewTab.Closed));
    }
}