using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Auth;
using UniFlow.Domain;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;
using UniFlow.Shared.Admin;

namespace UniFlow.Api.Admin;

// What the administrator does with accounts. Every change of permissions or personal data
// lands in the audit log, in the same transaction as the change itself.
public sealed class AccountService
{
    private readonly UniFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountService(UniFlowDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // What the form itself cannot know: an e-mail or album number another account already
    // has, a faculty that does not exist. Field -> message, empty when all is well.
    public async Task<Dictionary<string, string>> CheckAsync(AccountForm form, string? accountId,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();

        // The e-mail is also the login (UserName), so it has to be unique
        var normalizedEmail = _userManager.NormalizeEmail(form.Email);
        if (await _db.Users.AnyAsync(u => u.Id != accountId
                && (u.NormalizedEmail == normalizedEmail || u.NormalizedUserName == normalizedEmail),
                cancellationToken))
            errors[nameof(AccountForm.Email)] = "Ten e-mail ma już inne konto.";

        if (form.AlbumNumber is not null
            && await _db.StudentProfiles.AnyAsync(p => p.UserId != accountId
                && p.AlbumNumber == form.AlbumNumber, cancellationToken))
            errors[nameof(AccountForm.AlbumNumber)] = "Ten numer albumu ma już inny student.";

        var existing = await _db.Faculties.CountAsync(f => form.FacultyIds.Contains(f.Id),
            cancellationToken);
        if (existing != form.FacultyIds.Count)
            errors[nameof(AccountForm.FacultyIds)] = "Wybrany wydział nie istnieje. Odśwież stronę.";

        // A switched-off faculty may stay with an employee who has it, but nobody gets it anew
        var assigned = _db.StaffAssignments.Where(s => s.UserId == accountId).Select(s => s.FacultyId);
        if (await _db.Faculties.AnyAsync(f => form.FacultyIds.Contains(f.Id) && !f.IsActive
                && !assigned.Contains(f.Id), cancellationToken))
            errors[nameof(AccountForm.FacultyIds)] = "Wybrany wydział jest wyłączony.";

        return errors;
    }

    // A new account with a temporary password - the user sets their own at the first login
    public async Task<IssuedPassword> CreateAsync(string actorId, AccountForm form,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var password = TemporaryPasswords.Generate();
        var user = new ApplicationUser
        {
            UserName = form.Email,
            Email = form.Email,
            EmailConfirmed = true,
            FirstName = form.FirstName,
            LastName = form.LastName,
            CreatedAt = now,
            MustChangePassword = true,
        };

        // UserManager saves after each of its calls - the transaction makes it all or nothing
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        Check(await _userManager.CreateAsync(user, password));
        Check(await _userManager.AddToRolesAsync(user, form.Roles));

        if (form.AlbumNumber is not null)
            _db.StudentProfiles.Add(new StudentProfile(user.Id, form.AlbumNumber));

        Log(actorId, AuditAction.UserCreated, user.Id, user.Email, now);
        foreach (var role in form.Roles)
            Log(actorId, AuditAction.RoleAssigned, user.Id, role, now);

        foreach (var faculty in await FacultiesAsync(form.FacultyIds, cancellationToken))
        {
            _db.StaffAssignments.Add(new StaffAssignment(user.Id, faculty));
            Log(actorId, AuditAction.StaffAssigned, user.Id, faculty.Code, now);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new IssuedPassword(user.Id, password);
    }

    // Name, e-mail, album number, roles and faculties - only what has changed is saved and logged
    public async Task UpdateAsync(string actorId, ApplicationUser user, AccountForm form,
        CancellationToken cancellationToken)
    {
        // The acting administrator stays one - so there is always at least one
        if (user.Id == actorId && !form.Has(RoleNames.Administrator))
            throw new DomainException(DomainErrorCode.OwnAccountProtected,
                "Administrators cannot remove their own administrator role.");

        var now = DateTime.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // In a real deployment the data would come from the university's system; here the
        // administrator corrects it by hand
        var oldName = $"{user.FirstName} {user.LastName}";
        var newName = $"{form.FirstName} {form.LastName}";
        if (oldName != newName)
        {
            user.FirstName = form.FirstName;
            user.LastName = form.LastName;
            Log(actorId, AuditAction.PersonalDataCorrected, user.Id, $"{oldName} → {newName}", now);
        }

        if (user.Email != form.Email)
        {
            Log(actorId, AuditAction.LoginEmailChanged, user.Id, $"{user.Email} → {form.Email}", now);
            user.Email = form.Email;
            user.UserName = form.Email;
        }

        // Also sets the normalized e-mail and login used to look the user up
        Check(await _userManager.UpdateAsync(user));

        await UpdateRolesAsync(actorId, user, form.Roles, now);
        await UpdateAlbumNumberAsync(actorId, user.Id, form.AlbumNumber, now, cancellationToken);
        await UpdateFacultiesAsync(actorId, user.Id, form.FacultyIds, now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetActiveAsync(string actorId, ApplicationUser user, bool active)
    {
        if (user.IsActive == active)
            return;

        if (!active && user.Id == actorId)
            throw new DomainException(DomainErrorCode.OwnAccountProtected,
                "Administrators cannot deactivate their own account.");

        user.IsActive = active;
        Log(actorId, active ? AuditAction.UserActivated : AuditAction.UserDeactivated, user.Id,
            null, DateTime.UtcNow);

        // A new security stamp ends the user's sessions at the next check (within 5 minutes).
        // It saves the whole DbContext - the log entry included.
        Check(await _userManager.UpdateSecurityStampAsync(user));
    }

    // A new temporary password - for a user who forgot theirs. Unlocks the account too,
    // in case it was locked after wrong passwords.
    public async Task<IssuedPassword> ResetPasswordAsync(string actorId, ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var password = TemporaryPasswords.Generate();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        user.MustChangePassword = true;
        Check(await _userManager.RemovePasswordAsync(user));
        Check(await _userManager.AddPasswordAsync(user, password));
        Check(await _userManager.SetLockoutEndDateAsync(user, null));
        Check(await _userManager.ResetAccessFailedCountAsync(user));

        Log(actorId, AuditAction.PasswordReset, user.Id, null, DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new IssuedPassword(user.Id, password);
    }

    // Changed roles reach the user's cookie at the next security stamp check (within 5 minutes)
    private async Task UpdateRolesAsync(string actorId, ApplicationUser user,
        IReadOnlyList<string> roles, DateTime now)
    {
        var current = await _userManager.GetRolesAsync(user);
        var added = roles.Except(current).ToList();
        var removed = current.Except(roles).ToList();

        if (added.Count > 0)
            Check(await _userManager.AddToRolesAsync(user, added));
        if (removed.Count > 0)
            Check(await _userManager.RemoveFromRolesAsync(user, removed));

        foreach (var role in added)
            Log(actorId, AuditAction.RoleAssigned, user.Id, role, now);
        foreach (var role in removed)
            Log(actorId, AuditAction.RoleRemoved, user.Id, role, now);
    }

    // A student gets a profile with the first album number; taking the role away keeps it -
    // their applications still point to it
    private async Task UpdateAlbumNumberAsync(string actorId, string userId, string? albumNumber,
        DateTime now, CancellationToken cancellationToken)
    {
        if (albumNumber is null)
            return;

        var profile = await _db.StudentProfiles.SingleOrDefaultAsync(p => p.UserId == userId,
            cancellationToken);

        if (profile is null)
        {
            _db.StudentProfiles.Add(new StudentProfile(userId, albumNumber));
        }
        else if (profile.AlbumNumber != albumNumber)
        {
            Log(actorId, AuditAction.PersonalDataCorrected, userId,
                $"nr albumu {profile.AlbumNumber} → {albumNumber}", now);
            profile.ChangeAlbumNumber(albumNumber);
        }
    }

    private async Task UpdateFacultiesAsync(string actorId, string userId,
        IReadOnlyList<int> facultyIds, DateTime now, CancellationToken cancellationToken)
    {
        var current = await _db.StaffAssignments
            .Include(s => s.Faculty)
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var assignment in current.Where(s => !facultyIds.Contains(s.FacultyId)))
        {
            _db.StaffAssignments.Remove(assignment);
            Log(actorId, AuditAction.StaffUnassigned, userId, assignment.Faculty.Code, now);
        }

        var newIds = facultyIds.Except(current.Select(s => s.FacultyId)).ToList();
        foreach (var faculty in await FacultiesAsync(newIds, cancellationToken))
        {
            _db.StaffAssignments.Add(new StaffAssignment(userId, faculty));
            Log(actorId, AuditAction.StaffAssigned, userId, faculty.Code, now);
        }
    }

    private Task<List<Faculty>> FacultiesAsync(IReadOnlyList<int> ids,
        CancellationToken cancellationToken) =>
        _db.Faculties.Where(f => ids.Contains(f.Id)).ToListAsync(cancellationToken);

    // Details: a role is kept as its name (e.g. "ViceDean") and a faculty as its code -
    // the log page turns them into Polish labels
    private void Log(string actorId, AuditAction action, string targetUserId, string? details,
        DateTime now) =>
        _db.AuditLogEntries.Add(new AuditLogEntry(actorId, action, targetUserId, details, now));

    // CheckAsync has already ruled out what the administrator could cause - a failure here
    // is a bug, not a wrong form
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Identity operation failed: " +
                string.Join("; ", result.Errors.Select(e => e.Code)));
    }
}