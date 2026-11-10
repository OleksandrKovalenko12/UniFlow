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

namespace UniFlow.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Student)]
public sealed class ApplicationsController : ControllerBase
{
    public const string SubmissionRateLimit = "submissions";
    private readonly ApplicationSubmissionService _submission;
    private readonly DocumentRenderer _renderer;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationsController(ApplicationSubmissionService submission,
        DocumentRenderer renderer, UserManager<ApplicationUser> userManager)
    {
        _submission = submission;
        _renderer = renderer;
        _userManager = userManager;
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
}