using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UniFlow.Api.Applications;
using UniFlow.Api.Texts;
using UniFlow.Documents.Models;
using UniFlow.Documents.Rendering;
using UniFlow.Documents.Templates;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared;
using UniFlow.Shared.Applications;
using UniFlow.Shared.ApplicationTypes;
using UniFlow.Shared.Forms;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using UniFlow.Domain;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Infrastructure.Storage;

namespace UniFlow.Api.Controllers;

// Roles per endpoint: only a student submits, but staff of the faculty may read the case too
[ApiController]
public sealed class ApplicationsController : ControllerBase
{
    public const string SubmissionRateLimit = "submissions";
    private const string Readers =
        RoleNames.Student + "," + RoleNames.DeanOffice + "," + RoleNames.ViceDean;

    private readonly ApplicationAccess _access;
    private readonly ApplicationDetailsQuery _details;
    private readonly ApplicationSubmissionService _submission;
    private readonly DocumentRenderer _renderer;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UniFlowDbContext _db;
    private readonly IFileStorage _storage;

    public ApplicationsController(ApplicationSubmissionService submission, DocumentRenderer renderer,
    UserManager<ApplicationUser> userManager, UniFlowDbContext db, IFileStorage storage,
    ApplicationAccess access, ApplicationDetailsQuery details)
    {
        _access = access;
        _details = details;
        _submission = submission;
        _renderer = renderer;
        _userManager = userManager;
        _db = db;
        _storage = storage;
    }

    // The form has already been validated by [ApiController]. draftId: the files added to the
    // draft come along as attachments, and the draft goes.
    [HttpPost("api/enrollments/{enrollmentId:int}/applications/group-change")]
    [Authorize(Roles = RoleNames.Student)]
    [EnableRateLimiting(SubmissionRateLimit)]
    public async Task<ActionResult<SubmittedApplication>> SubmitGroupChange(int enrollmentId,
    [FromHeader(Name = SubmissionHeaders.IdempotencyKey), Required] Guid? submissionKey,
    [FromQuery] Guid? draftId, GroupChangeForm form, CancellationToken cancellationToken)
    {
        form.Normalize();

        var submitted = await _submission.SubmitAsync(
            _userManager.GetUserId(User)!,
            submissionKey!.Value,
            enrollmentId,
            ApplicationTypeCodes.GroupChange,
            FormJson.Serialize(form), 
            draftId,
            header => _renderer.RenderAsync<GroupChangeTemplate, GroupChangeDocument>(
                new GroupChangeDocument(header, form)),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, submitted);
    }

    // The student's own applications: those waiting for them first, then the newest
    [HttpGet("api/applications/mine")]
    [Authorize(Roles = RoleNames.Student)]
    public async Task<IReadOnlyList<MyApplicationItem>> GetMine(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        var rows = await _db.Applications
            .Where(a => a.ApplicantId == userId)
            .OrderByDescending(a => a.Status == ApplicationStatus.NeedsCorrection)
            .ThenByDescending(a => a.CreatedAt)
                        .Select(a => new
                        {
                            a.Id,
                            a.Number,
                            TypeCode = a.ApplicationType.Code,
                            TypeName = a.ApplicationType.Name,
                            a.Status,
                            a.CreatedAt,
                            a.EnrollmentId,
                            FieldOfStudyName = a.Enrollment.FieldOfStudy.Name,
                            a.Enrollment.FieldOfStudy.Level
                        })
            .ToListAsync(cancellationToken);

        // "In progress" is decided here, by Domain - the browser does not repeat the rule
        return rows
            .Select(r => new MyApplicationItem(r.Id, r.Number, r.TypeCode, r.TypeName,
                (ApplicationState)r.Status, Application.InProgressStatuses.Contains(r.Status),
                r.CreatedAt, r.EnrollmentId, r.FieldOfStudyName, StudyTexts.Level(r.Level)))
            .ToList();
    }

    // Only the author; Domain decides whether it is still possible
    [HttpPost("api/applications/{id:guid}/withdraw")]
    [Authorize(Roles = RoleNames.Student)]
    public async Task<ActionResult<ApplicationDetails>> Withdraw(Guid id,
        CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User)!;

        var application = await _db.Applications
            .SingleOrDefaultAsync(a => a.Id == id && a.ApplicantId == userId, cancellationToken);

        if (application is null)
            return NotFound();

        application.Withdraw(new Actor(userId, ActorRole.Student), DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return await _details.GetAsync(id, ApplicationViewer.Applicant, User, cancellationToken);
    }


    // Someone else's application looks like a missing one (404, not 403)
    [HttpGet("api/applications/{id:guid}")]
    [Authorize(Roles = Readers)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApplicationDetails>> GetDetails(Guid id,
        CancellationToken cancellationToken)
    {
        var viewer = await _access.GetViewerAsync(id, User, cancellationToken);
        if (viewer is null)
            return NotFound();

        return await _details.GetAsync(id, viewer.Value, User, cancellationToken);
    }

    // The current PDF of the application
    [HttpGet("api/applications/{id:guid}/document")]
    [Authorize(Roles = Readers)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetDocument(Guid id, bool inline,
        CancellationToken cancellationToken)
    {
        if (await _access.GetViewerAsync(id, User, cancellationToken) is null)
            return NotFound();

        var document = await _db.Applications
            .Where(a => a.Id == id)
            .SelectMany(a => a.Attachments)
            .Where(at => at.Kind == AttachmentKind.GeneratedDocument && !at.IsArchived)
            .Select(at => new { at.StoragePath, at.OriginalFileName })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
            return NotFound();

        return this.PdfFile(_storage, document.StoragePath, document.OriginalFileName, inline);
    }

    // Always the PDF version: a file from a student is never opened in the browser as it is
    // Staff may also open removed files and older versions of the application's PDF.
    [HttpGet("api/applications/{id:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Roles = Readers)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetAttachment(Guid id, Guid attachmentId, bool inline,
        CancellationToken cancellationToken)
    {
        var viewer = await _access.GetViewerAsync(id, User, cancellationToken);
        if (viewer is null)
            return NotFound();

        var staff = viewer == ApplicationViewer.Staff;

        var attachment = await _db.Applications
            .Where(a => a.Id == id)
            .SelectMany(a => a.Attachments)
            .Where(at => at.Id == attachmentId && (staff || !at.IsArchived))
            .Select(at => new { Path = at.PdfStoragePath ?? at.StoragePath, at.OriginalFileName })
            .SingleOrDefaultAsync(cancellationToken);

        if (attachment is null)
            return NotFound();

        return this.PdfFile(_storage, attachment.Path, Path.ChangeExtension(attachment.OriginalFileName, ".pdf"), inline);
    }

}