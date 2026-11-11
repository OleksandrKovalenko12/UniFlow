using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UniFlow.Api.Applications;
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
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Infrastructure.Storage;

namespace UniFlow.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Student)]
public sealed class ApplicationsController : ControllerBase
{
    public const string SubmissionRateLimit = "submissions";
    public const string UploadRateLimit = "uploads";
    private readonly AttachmentUploadService _attachments;
    private readonly ApplicationSubmissionService _submission;
    private readonly DocumentRenderer _renderer;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UniFlowDbContext _db;
    private readonly IFileStorage _storage;

    public ApplicationsController(ApplicationSubmissionService submission,
    AttachmentUploadService attachments, DocumentRenderer renderer,
    UserManager<ApplicationUser> userManager, UniFlowDbContext db, IFileStorage storage)
    {
        _submission = submission;
        _attachments = attachments;
        _renderer = renderer;
        _userManager = userManager;
        _db = db;
        _storage = storage;
    }

    // The form has already been validated by [ApiController]
    [HttpPost("api/enrollments/{enrollmentId:int}/applications/group-change")]
    [EnableRateLimiting(SubmissionRateLimit)]
    public async Task<ActionResult<SubmittedApplication>> SubmitGroupChange(int enrollmentId,
        [FromHeader(Name = SubmissionHeaders.IdempotencyKey), Required] Guid? submissionKey,
        GroupChangeForm form, CancellationToken cancellationToken)
    {
        form.Normalize();

        var submitted = await _submission.SubmitAsync(
            _userManager.GetUserId(User)!,
            submissionKey!.Value,
            enrollmentId,
            ApplicationTypeCodes.GroupChange,
            FormJson.Serialize(form),
            header => _renderer.RenderAsync<GroupChangeTemplate, GroupChangeDocument>(
                new GroupChangeDocument(header, form)),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, submitted);
    }

    // The owner is part of the query: someone else's application looks like a missing one
    [HttpGet("api/applications/{id:guid}/document")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetDocument(Guid id, bool inline,
        CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        var document = await _db.Applications
            .Where(a => a.Id == id && a.ApplicantId == userId)
            .SelectMany(a => a.Attachments)
            .Where(at => at.Kind == AttachmentKind.GeneratedDocument && !at.IsArchived)
            .Select(at => new { at.StoragePath, at.OriginalFileName })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
            return NotFound();

        return PdfFile(document.StoragePath, document.OriginalFileName, inline);
    }

    // One file per request - the Client sends them one after another.
    // The size limit stops a too large upload before the whole file is received.
    [HttpPost("api/applications/{id:guid}/attachments")]
    [EnableRateLimiting(UploadRateLimit)]
    [RequestSizeLimit(AttachmentRules.MaxSizeBytes + 1024 * 1024)]
    public async Task<ActionResult<AttachmentSummary>> AddAttachment(Guid id, IFormFile file,
        CancellationToken cancellationToken)
    {
        var added = await _attachments.UploadAsync(_userManager.GetUserId(User)!, id, file,
            cancellationToken);

        return added is null ? NotFound() : StatusCode(StatusCodes.Status201Created, added);
    }

    // Always the PDF version: a file from a student is never opened in the browser as it is
    [HttpGet("api/applications/{id:guid}/attachments/{attachmentId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetAttachment(Guid id, Guid attachmentId, bool inline,
        CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        var attachment = await _db.Applications
            .Where(a => a.Id == id && a.ApplicantId == userId)
            .SelectMany(a => a.Attachments)
            .Where(at => at.Id == attachmentId && at.Kind == AttachmentKind.StudentUpload
                && !at.IsArchived)
            .Select(at => new { Path = at.PdfStoragePath ?? at.StoragePath, at.OriginalFileName })
            .SingleOrDefaultAsync(cancellationToken);

        if (attachment is null)
            return NotFound();

        return PdfFile(attachment.Path, Path.ChangeExtension(attachment.OriginalFileName, ".pdf"),
            inline);
    }

    private FileStreamResult PdfFile(string storagePath, string fileName, bool inline)
    {
        // "inline" opens the PDF in the browser, "attachment" saves it
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(fileName);
        Response.Headers.ContentDisposition = disposition.ToString();

        // The browser must not guess another type than the one we send
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(_storage.OpenRead(storagePath), Attachment.PdfContentType);
    }
}