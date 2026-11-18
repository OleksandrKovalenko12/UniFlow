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

// Faculties and fields of study. Nothing is deleted, only switched off - studies, applications
// and staff point to them. Every change goes to the audit log; every answer is the whole
// structure as it is now.
[ApiController]
[Route("api/admin")]
[Authorize(Roles = RoleNames.Administrator)]
public sealed class AdminStructureController : ControllerBase
{
    private readonly UniFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminStructureController(UniFlowDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("structure")]
    public Task<IReadOnlyList<FacultyItem>> GetStructure(CancellationToken cancellationToken) =>
        StructureAsync(cancellationToken);

    [HttpPost("faculties")]
    public async Task<ActionResult<IReadOnlyList<FacultyItem>>> AddFaculty(FacultyForm form,
        CancellationToken cancellationToken)
    {
        form.Normalize();
        if (await FacultyTakenAsync(form, facultyId: null, cancellationToken) is { } taken)
            return taken;

        _db.Faculties.Add(new Faculty(form.Code, form.Name));
        Log(AuditAction.FacultyCreated, $"{form.Code} - {form.Name}");
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await StructureAsync(cancellationToken));
    }

    // The code may change too - lists show the new one, older log entries keep the old one
    [HttpPut("faculties/{id:int}")]
    public async Task<ActionResult<IReadOnlyList<FacultyItem>>> UpdateFaculty(int id, FacultyForm form,
        CancellationToken cancellationToken)
    {
        var faculty = await _db.Faculties.SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (faculty is null)
            return NotFound();

        form.Normalize();
        if (await FacultyTakenAsync(form, id, cancellationToken) is { } taken)
            return taken;

        // A closed faculty with an open field of study would still take new students
        if (faculty.IsActive && !form.IsActive
            && await _db.FieldsOfStudy.AnyAsync(f => f.FacultyId == id && f.IsActive, cancellationToken))
            return FieldError(nameof(form.IsActive), "Najpierw wyłącz wszystkie kierunki tego wydziału.");

        var changes = new List<string>();
        var oldCode = faculty.Code;

        if (faculty.Name != form.Name)
        {
            changes.Add($"nazwa → {form.Name}");
            faculty.Rename(form.Name);
        }

        if (faculty.Code != form.Code)
        {
            changes.Add($"kod → {form.Code}");
            faculty.ChangeCode(form.Code);
        }

        if (faculty.IsActive != form.IsActive)
        {
            changes.Add(form.IsActive ? "włączony" : "wyłączony");
            if (form.IsActive)
                faculty.Activate();
            else
                faculty.Deactivate();
        }

        if (changes.Count > 0)
        {
            Log(AuditAction.FacultyChanged, $"{oldCode} ({string.Join(", ", changes)})");
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(await StructureAsync(cancellationToken));
    }

    [HttpPost("fields-of-study")]
    public async Task<ActionResult<IReadOnlyList<FacultyItem>>> AddFieldOfStudy(FieldOfStudyForm form,
        CancellationToken cancellationToken)
    {
        form.Normalize();

        var faculty = await _db.Faculties.SingleOrDefaultAsync(f => f.Id == form.FacultyId, cancellationToken);
        if (faculty is null)
            return FieldError(nameof(form.FacultyId), "Wybrany wydział nie istnieje. Odśwież stronę.");
        if (!faculty.IsActive)
            return FieldError(nameof(form.FacultyId), "Ten wydział jest wyłączony.");

        var level = (StudyLevel)form.Level;
        if (await FieldTakenAsync(form.Name, level, faculty.Id, fieldId: null, cancellationToken))
            return FieldError(nameof(form.Name), "Ten kierunek na tym poziomie już jest na tym wydziale.");

        _db.FieldsOfStudy.Add(new FieldOfStudy(form.Name, level, faculty));
        Log(AuditAction.FieldOfStudyCreated, $"{form.Name}, {StudyTexts.Level(level)} ({faculty.Code})");
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await StructureAsync(cancellationToken));
    }

    // The level is not changed here - it decides the semesters of everyone's studies on this
    // field, so form.Level is ignored. Moving a field moves all its students' applications - in
    // progress and closed - to the dean's office of the new faculty; the page warns about it.
    [HttpPut("fields-of-study/{id:int}")]
    public async Task<ActionResult<IReadOnlyList<FacultyItem>>> UpdateFieldOfStudy(int id,
        FieldOfStudyForm form, CancellationToken cancellationToken)
    {
        var field = await _db.FieldsOfStudy
            .Include(f => f.Faculty)
            .SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (field is null)
            return NotFound();

        form.Normalize();

        var faculty = await _db.Faculties.SingleOrDefaultAsync(f => f.Id == form.FacultyId, cancellationToken);
        if (faculty is null)
            return FieldError(nameof(form.FacultyId), "Wybrany wydział nie istnieje. Odśwież stronę.");

        // Only a switched-on faculty takes a field (staying where it is, is always fine)
        if (!faculty.IsActive && faculty.Id != field.FacultyId)
            return FieldError(nameof(form.FacultyId), "Ten wydział jest wyłączony.");
        if (form.IsActive && !faculty.IsActive)
            return FieldError(nameof(form.IsActive), "Wydział jest wyłączony - najpierw go włącz.");

        if (await FieldTakenAsync(form.Name, field.Level, faculty.Id, id, cancellationToken))
            return FieldError(nameof(form.Name), "Ten kierunek na tym poziomie już jest na tym wydziale.");

        var changes = new List<string>();
        var oldName = $"{field.Name}, {StudyTexts.Level(field.Level)}";

        if (field.Name != form.Name)
        {
            changes.Add($"nazwa → {form.Name}");
            field.Rename(form.Name);
        }

        if (field.FacultyId != faculty.Id)
        {
            changes.Add($"wydział {field.Faculty.Code} → {faculty.Code}");
            field.MoveToFaculty(faculty);
        }

        if (field.IsActive != form.IsActive)
        {
            changes.Add(form.IsActive ? "włączony" : "wyłączony");
            if (form.IsActive)
                field.Activate();
            else
                field.Deactivate();
        }

        if (changes.Count > 0)
        {
            Log(AuditAction.FieldOfStudyChanged, $"{oldName} ({string.Join(", ", changes)})");
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(await StructureAsync(cancellationToken));
    }

    // Codes and names are compared without case - "wiit" and "WIiT" would be confused in lists
    private async Task<ActionResult?> FacultyTakenAsync(FacultyForm form, int? facultyId,
        CancellationToken cancellationToken)
    {
        var code = form.Code.ToLower();
        if (await _db.Faculties.AnyAsync(f => f.Id != facultyId && f.Code.ToLower() == code, cancellationToken))
            return FieldError(nameof(form.Code), "Ten kod ma już inny wydział.");

        var name = form.Name.ToLower();
        if (await _db.Faculties.AnyAsync(f => f.Id != facultyId && f.Name.ToLower() == name, cancellationToken))
            return FieldError(nameof(form.Name), "Wydział o tej nazwie już jest.");

        return null;
    }

    private Task<bool> FieldTakenAsync(string name, StudyLevel level, int facultyId, int? fieldId,
        CancellationToken cancellationToken)
    {
        var lowerName = name.ToLower();
        return _db.FieldsOfStudy.AnyAsync(f => f.Id != fieldId && f.FacultyId == facultyId
            && f.Level == level && f.Name.ToLower() == lowerName, cancellationToken);
    }

    private void Log(AuditAction action, string details) =>
        _db.AuditLogEntries.Add(new AuditLogEntry(_userManager.GetUserId(User)!, action,
            targetUserId: null, details, DateTime.UtcNow));

    // Switched-off ones too - the page shows them on request
    private async Task<IReadOnlyList<FacultyItem>> StructureAsync(CancellationToken cancellationToken)
    {
        var faculties = await _db.Faculties
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Code, f.Name, f.IsActive })
            .ToListAsync(cancellationToken);

        var fields = await _db.FieldsOfStudy
            .OrderBy(f => f.Name)
            .ThenBy(f => f.Level)
            .Select(f => new { f.Id, f.FacultyId, f.Name, f.Level, f.IsActive })
            .ToListAsync(cancellationToken);

        return faculties
            .Select(faculty => new FacultyItem(faculty.Id, faculty.Code, faculty.Name, faculty.IsActive, fields
                .Where(f => f.FacultyId == faculty.Id)
                .Select(f => new FieldOfStudyItem(f.Id, f.FacultyId, f.Name, (StudyLevelOption)f.Level,
                    StudyTexts.Level(f.Level), f.IsActive))
                .ToList()))
            .ToList();
    }

    // The same answer as a form rejected by [ApiController] - the error lands under the field
    private ActionResult FieldError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}