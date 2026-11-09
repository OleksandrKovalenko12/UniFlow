using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Texts;
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
    public async Task<IReadOnlyList<EnrollmentSummary>> GetMine(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        var enrollments = await _db.Enrollments
            .Where(e => e.StudentId == userId && e.IsActive)
            .OrderBy(e => e.FieldOfStudy.Name)
            .ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                FacultyName = e.FieldOfStudy.Faculty.Name,
                FieldOfStudyName = e.FieldOfStudy.Name,
                e.FieldOfStudy.Level,
                e.StudyMode,
                e.Semester,
            })
            .ToListAsync(cancellationToken);

        return enrollments
            .Select(e => new EnrollmentSummary(e.Id, e.FacultyName, e.FieldOfStudyName,
                StudyTexts.Level(e.Level), StudyTexts.Mode(e.StudyMode), e.Semester))
                .ToList();
    }
}