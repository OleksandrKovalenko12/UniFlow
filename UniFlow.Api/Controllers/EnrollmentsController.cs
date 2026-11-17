using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UniFlow.Api.Enrollments;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;
using UniFlow.Shared.Enrollments;

namespace UniFlow.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize(Roles = RoleNames.Student)]
public sealed class EnrollmentsController : ControllerBase
{
    private readonly UniFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public EnrollmentsController(UniFlowDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Only the logged-in student's own active enrollments - the user id comes from the
    // cookie, not from the URL, so there is nothing to swap to see someone else's data
    [HttpGet("mine")]
    public Task<IReadOnlyList<EnrollmentSummary>> GetMine(CancellationToken cancellationToken) =>
        EnrollmentSummaries.ActiveOfAsync(_db, _userManager.GetUserId(User)!, cancellationToken);
}