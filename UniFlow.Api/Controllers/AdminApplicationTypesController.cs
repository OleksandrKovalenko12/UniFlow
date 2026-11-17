using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;
using UniFlow.Shared.Admin;

namespace UniFlow.Api.Controllers;

// The kinds of applications: name, description, attachment limit, the vice-dean's decision and
// whether new ones can be submitted. An application copies the limit and the decision path when
// it is submitted, so a change here applies to new applications only.
[ApiController]
[Route("api/admin/application-types")]
[Authorize(Roles = RoleNames.Administrator)]
public sealed class AdminApplicationTypesController : ControllerBase
{
    private readonly UniFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminApplicationTypesController(UniFlowDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IReadOnlyList<ApplicationTypeDetails>> GetList(CancellationToken cancellationToken) =>
        await _db.ApplicationTypes
            .OrderBy(t => t.Name)
            .Select(t => new ApplicationTypeDetails(t.Id, t.Code, t.Name, t.Description,
                t.RequiresDeanDecision, t.MaxAttachments, t.IsActive))
            .ToListAsync(cancellationToken);

    // The form has already been validated by [ApiController]. One log entry for the whole save,
    // listing what has changed.
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApplicationTypeDetails>> Update(int id, ApplicationTypeForm form,
        CancellationToken cancellationToken)
    {
        var type = await _db.ApplicationTypes.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (type is null)
            return NotFound();

        form.Normalize();
        var oldName = type.Name;
        var changes = new List<string>();

        if (type.Name != form.Name)
        {
            changes.Add($"nazwa → {form.Name}");
            type.Rename(form.Name);
        }

        if (type.Description != form.Description)
        {
            changes.Add("opis");
            type.ChangeDescription(form.Description);
        }

        if (type.MaxAttachments != form.MaxAttachments)
        {
            changes.Add($"limit załączników {type.MaxAttachments} → {form.MaxAttachments}");
            type.SetMaxAttachments(form.MaxAttachments);
        }

        if (type.RequiresDeanDecision != form.RequiresDeanDecision)
        {
            changes.Add(form.RequiresDeanDecision ? "z decyzją prodziekana" : "bez decyzji prodziekana");
            type.SetRequiresDeanDecision(form.RequiresDeanDecision);
        }

        if (type.IsActive != form.IsActive)
        {
            changes.Add(form.IsActive ? "włączony" : "wyłączony");
            if (form.IsActive)
                type.Activate();
            else
                type.Deactivate();
        }

        // "Zmiana grupy zajęciowej (limit załączników 2 → 3, wyłączony)"
        if (changes.Count > 0)
        {
            _db.AuditLogEntries.Add(new AuditLogEntry(_userManager.GetUserId(User)!,
                AuditAction.ApplicationTypeChanged, targetUserId: null,
                $"{oldName} ({string.Join(", ", changes)})", DateTime.UtcNow));
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new ApplicationTypeDetails(type.Id, type.Code, type.Name, type.Description,
            type.RequiresDeanDecision, type.MaxAttachments, type.IsActive);
    }
}