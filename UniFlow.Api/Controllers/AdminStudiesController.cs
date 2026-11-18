using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Texts;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;
using UniFlow.Shared.Admin;

namespace UniFlow.Api.Controllers;

// A student's studies, managed by the administrator - in a real deployment they would come from
// the university's system. The faculty follows from the field of study. Not in the audit log:
// semesters and modes are study routine, not permissions.
// Every change returns the whole list - the page shows it as it is now.
[ApiController]
[Route("api/admin/accounts/{accountId}/studies")]
[Authorize(Roles = RoleNames.Administrator)]
public sealed class AdminStudiesController : ControllerBase
{
    private readonly UniFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminStudiesController(UniFlowDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StudiesItem>>> GetList(string accountId,
        CancellationToken cancellationToken)
    {
        if (!await IsStudentAsync(accountId))
            return NotFound();

        return Ok(await ListAsync(accountId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<StudiesItem>>> Add(string accountId,
        AddStudiesRequest request, CancellationToken cancellationToken)
    {
        if (!await IsStudentAsync(accountId))
            return NotFound();

        var fieldOfStudy = await _db.FieldsOfStudy
            .SingleOrDefaultAsync(f => f.Id == request.FieldOfStudyId, cancellationToken);
        if (fieldOfStudy is null)
            return FieldError(nameof(request.FieldOfStudyId), "Wybrany kierunek nie istnieje. Odśwież stronę.");

        // Phased out - students already on it finish, nobody new starts
        if (!fieldOfStudy.IsActive)
            return FieldError(nameof(request.FieldOfStudyId), "Ten kierunek jest wyłączony.");

        // One studies per field of study and student - ended ones are taken up again, so the
        // history of their applications stays in one place
        var existing = await _db.Enrollments
            .Where(e => e.StudentId == accountId && e.FieldOfStudyId == fieldOfStudy.Id)
            .Select(e => (bool?)e.IsActive)
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return FieldError(nameof(request.FieldOfStudyId), existing.Value
                ? "Student ma już te studia."
                : "Te studia są zakończone - wznów je na liście.");

        _db.Enrollments.Add(new Enrollment(accountId, fieldOfStudy, request.Semester,
            (StudyMode)request.StudyMode));
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await ListAsync(accountId, cancellationToken));
    }

    // Domain refuses changes to ended studies (409)
    [HttpPut("{id:int}")]
    public Task<ActionResult<IReadOnlyList<StudiesItem>>> Change(string accountId, int id,
        ChangeStudiesRequest request, CancellationToken cancellationToken) =>
        UpdateAsync(accountId, id, enrollment =>
        {
            enrollment.ChangeSemester(request.Semester);
            enrollment.ChangeStudyMode((StudyMode)request.StudyMode);
        }, cancellationToken);

    // Applications in progress go on; new ones on these studies are refused by Domain
    [HttpPost("{id:int}/end")]
    public Task<ActionResult<IReadOnlyList<StudiesItem>>> End(string accountId, int id,
        CancellationToken cancellationToken) =>
        UpdateAsync(accountId, id, enrollment => enrollment.Deactivate(), cancellationToken);

    [HttpPost("{id:int}/resume")]
    public Task<ActionResult<IReadOnlyList<StudiesItem>>> Resume(string accountId, int id,
        ResumeStudiesRequest request, CancellationToken cancellationToken) =>
        UpdateAsync(accountId, id, enrollment => enrollment.Reactivate(request.Semester),
            cancellationToken);

    // For the "Dodaj studia" dialog - only switched-on fields of study
    [HttpGet("/api/admin/fields-of-study")]
    public async Task<IReadOnlyList<FieldOfStudyOption>> GetFieldsOfStudy(
        CancellationToken cancellationToken)
    {
        var fields = await _db.FieldsOfStudy
            .Where(f => f.IsActive)
            .OrderBy(f => f.Faculty.Name)
            .ThenBy(f => f.Name)
            .ThenBy(f => f.Level)
            .Select(f => new { f.Id, f.Name, f.Level, FacultyCode = f.Faculty.Code })
            .ToListAsync(cancellationToken);

        return fields
            .Select(f => new FieldOfStudyOption(f.Id, f.Name, StudyTexts.Level(f.Level),
                f.FacultyCode, Enrollment.MaxSemester(f.Level)))
            .ToList();
    }

    private async Task<bool> IsStudentAsync(string accountId)
    {
        var user = await _userManager.FindByIdAsync(accountId);
        return user is not null && await _userManager.IsInRoleAsync(user, RoleNames.Student);
    }

    private async Task<ActionResult<IReadOnlyList<StudiesItem>>> UpdateAsync(string accountId,
        int id, Action<Enrollment> change, CancellationToken cancellationToken)
    {
        // Enrollment's methods check the semester against the level - FieldOfStudy is needed
        var enrollment = await _db.Enrollments
            .Include(e => e.FieldOfStudy)
            .SingleOrDefaultAsync(e => e.Id == id && e.StudentId == accountId, cancellationToken);
        if (enrollment is null)
            return NotFound();

        change(enrollment);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await ListAsync(accountId, cancellationToken));
    }

    // Active studies first, then by name
    private async Task<IReadOnlyList<StudiesItem>> ListAsync(string accountId,
        CancellationToken cancellationToken)
    {
        var enrollments = await _db.Enrollments
            .Where(e => e.StudentId == accountId)
            .OrderByDescending(e => e.IsActive)
            .ThenBy(e => e.FieldOfStudy.Name)
            .Select(e => new
            {
                e.Id,
                e.FieldOfStudyId,
                FieldOfStudyName = e.FieldOfStudy.Name,
                e.FieldOfStudy.Level,
                FacultyName = e.FieldOfStudy.Faculty.Name,
                e.Semester,
                e.StudyMode,
                e.IsActive
            })
            .ToListAsync(cancellationToken);

        return enrollments
            .Select(e => new StudiesItem(e.Id, e.FieldOfStudyId, e.FieldOfStudyName,
                StudyTexts.Level(e.Level), e.FacultyName, e.Semester, Enrollment.MaxSemester(e.Level),
                (StudyModeOption)e.StudyMode, e.IsActive))
            .ToList();
    }

    // The same answer as a form rejected by [ApiController] - the error lands under the field
    private ActionResult FieldError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}