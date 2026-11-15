using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniFlow.Api.Applications;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Storage;
using UniFlow.Shared;
using UniFlow.Shared.Applications;
using UniFlow.Shared.Drafts;

namespace UniFlow.Api.Controllers;

// The student's unfinished applications: the form saved as it is typed, and the files added to it
[ApiController]
[Route("api/drafts")]
[Authorize(Roles = RoleNames.Student)]
public sealed class DraftsController : ControllerBase
{
    public const string UploadRateLimit = "uploads";

    private readonly DraftService _drafts;
    private readonly IFileStorage _storage;
    private readonly UserManager<ApplicationUser> _userManager;

    public DraftsController(DraftService drafts, IFileStorage storage,
        UserManager<ApplicationUser> userManager)
    {
        _drafts = drafts;
        _storage = storage;
        _userManager = userManager;
    }

    private string UserId => _userManager.GetUserId(User)!;

    [HttpGet]
    public Task<IReadOnlyList<DraftSummary>> GetMine(CancellationToken cancellationToken) =>
        _drafts.ListAsync(UserId, cancellationToken);

    // One draft per type, so the type is enough to find it. No draft is a normal answer
    // (204 No Content), not an error.
    [HttpGet("{typeCode}")]
    public async Task<IActionResult> Get(string typeCode, CancellationToken cancellationToken)
    {
        var draft = await _drafts.GetAsync(UserId, typeCode, cancellationToken);
        return draft is null ? NoContent() : Ok(draft);
    }

    [HttpPut("{typeCode}")]
    public Task<DraftDetails> Save(string typeCode, SaveDraftRequest request,
        CancellationToken cancellationToken) =>
        _drafts.SaveAsync(UserId, typeCode, request, cancellationToken);

    // The changes to an application sent back for correction - 204 when there are none yet
    [HttpGet("correction/{applicationId:guid}")]
    public async Task<IActionResult> GetCorrection(Guid applicationId,
        CancellationToken cancellationToken)
    {
        var draft = await _drafts.GetCorrectionAsync(UserId, applicationId, cancellationToken);
        return draft is null ? NoContent() : Ok(draft);
    }

    // Someone else's application looks like a missing one
    [HttpPut("correction/{applicationId:guid}")]
    public async Task<ActionResult<DraftDetails>> SaveCorrection(Guid applicationId,
        SaveCorrectionRequest request, CancellationToken cancellationToken)
    {
        var draft = await _drafts.SaveCorrectionAsync(UserId, applicationId, request, cancellationToken);
        return draft is null ? NotFound() : draft;
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _drafts.DeleteAsync(UserId, id, cancellationToken) ? NoContent() : NotFound();

    // One file per request. The size limit stops a too large upload before the whole file
    // is received.
    [HttpPost("{id:guid}/files")]
    [EnableRateLimiting(UploadRateLimit)]
    [RequestSizeLimit(AttachmentRules.MaxSizeBytes + 1024 * 1024)]
    public async Task<ActionResult<DraftFileInfo>> AddFile(Guid id, IFormFile file,
        CancellationToken cancellationToken)
    {
        var added = await _drafts.AddFileAsync(UserId, id, file, cancellationToken);
        return added is null ? NotFound() : StatusCode(StatusCodes.Status201Created, added);
    }

    [HttpDelete("{id:guid}/files/{fileId:guid}")]
    public async Task<IActionResult> RemoveFile(Guid id, Guid fileId,
        CancellationToken cancellationToken) =>
        await _drafts.RemoveFileAsync(UserId, id, fileId, cancellationToken) ? NoContent() : NotFound();

    // The PDF version - the same the dean's office will see after submission
    [HttpGet("{id:guid}/files/{fileId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetFile(Guid id, Guid fileId, bool inline,
        CancellationToken cancellationToken)
    {
        var file = await _drafts.FindFileAsync(UserId, id, fileId, cancellationToken);
        return file is null
            ? NotFound()
            : this.PdfFile(_storage, file.Value.Path, file.Value.FileName, inline);
    }
}