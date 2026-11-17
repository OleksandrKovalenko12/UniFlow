using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Admin;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;
using UniFlow.Shared.Admin;
using UniFlow.Shared.Review;

namespace UniFlow.Api.Controllers;

// The administrator's account management. Without an integration with the university's
// system (USOS) the administrator creates the accounts and corrects their data by hand.
[ApiController]
[Route("api/admin/accounts")]
[Authorize(Roles = RoleNames.Administrator)]
public sealed class AdminAccountsController : ControllerBase
{
    public const int PageSize = 25;

    private readonly UniFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AccountService _accounts;

    public AdminAccountsController(UniFlowDbContext db, UserManager<ApplicationUser> userManager,
        AccountService accounts)
    {
        _db = db;
        _userManager = userManager;
        _accounts = accounts;
    }

    // search: part of the name, e-mail or album number. role: one of RoleNames.
    // active: true = only active accounts, false = only deactivated ones.
    [HttpGet]
    public async Task<PagedList<AccountListItem>> GetList(string? search = null,
        string? role = null, bool? active = null, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).ToLower().Contains(term)
                || u.Email!.ToLower().Contains(term)
                || _db.StudentProfiles.Any(p => p.UserId == u.Id && p.AlbumNumber.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleId = _db.Roles.Where(r => r.Name == role).Select(r => r.Id);
            query = query.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id
                && roleId.Contains(ur.RoleId)));
        }

        if (active is not null)
            query = query.Where(u => u.IsActive == active);

        var totalCount = await query.CountAsync(cancellationToken);
        page = Math.Max(page, 1);

        var rows = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ThenBy(u => u.Email)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(u => new
            {
                u.Id,
                FullName = u.FirstName + " " + u.LastName,
                u.Email,
                u.IsActive,
                AlbumNumber = _db.StudentProfiles.Where(p => p.UserId == u.Id)
                    .Select(p => p.AlbumNumber).FirstOrDefault(),
                Roles = _db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new AccountListItem(r.Id, r.FullName, r.Email!, InOrder(r.Roles),
                r.AlbumNumber, r.IsActive))
            .ToList();

        return new PagedList<AccountListItem>(items, totalCount, page, PageSize);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountDetails>> Get(string id,
        CancellationToken cancellationToken)
    {
        var details = await DetailsAsync(id, cancellationToken);
        return details is null ? NotFound() : details;
    }

    // The form has already been validated by [ApiController]. Returns the temporary password -
    // the only time anyone sees it.
    [HttpPost]
    public async Task<ActionResult<IssuedPassword>> Create(AccountForm form,
        CancellationToken cancellationToken)
    {
        form.Normalize();

        var errors = await _accounts.CheckAsync(form, accountId: null, cancellationToken);
        if (errors.Count > 0)
            return FieldErrors(errors);

        var issued = await _accounts.CreateAsync(ActorId, form, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, issued);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AccountDetails>> Update(string id, AccountForm form,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        form.Normalize();

        var errors = await _accounts.CheckAsync(form, id, cancellationToken);
        if (errors.Count > 0)
            return FieldErrors(errors);

        await _accounts.UpdateAsync(ActorId, user, form, cancellationToken);
        return (await DetailsAsync(id, cancellationToken))!;
    }

    [HttpPost("{id}/activate")]
    public Task<ActionResult<AccountDetails>> Activate(string id,
        CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: true, cancellationToken);

    [HttpPost("{id}/deactivate")]
    public Task<ActionResult<AccountDetails>> Deactivate(string id,
        CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: false, cancellationToken);

    [HttpPost("{id}/reset-password")]
    public async Task<ActionResult<IssuedPassword>> ResetPassword(string id,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        return await _accounts.ResetPasswordAsync(ActorId, user, cancellationToken);
    }

    // For choosing an employee's faculties
    [HttpGet("/api/admin/faculties")]
    public async Task<IReadOnlyList<FacultyOption>> GetFaculties(
        CancellationToken cancellationToken) =>
        await _db.Faculties
            .OrderBy(f => f.Name)
            .Select(f => new FacultyOption(f.Id, f.Code, f.Name))
            .ToListAsync(cancellationToken);

    private string ActorId => _userManager.GetUserId(User)!;

    private async Task<ActionResult<AccountDetails>> SetActiveAsync(string id, bool active,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        await _accounts.SetActiveAsync(ActorId, user, active);
        return (await DetailsAsync(id, cancellationToken))!;
    }

    private async Task<AccountDetails?> DetailsAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id,
            cancellationToken);
        if (user is null)
            return null;

        var roles = await _db.UserRoles.Where(ur => ur.UserId == id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
            .ToListAsync(cancellationToken);

        var albumNumber = await _db.StudentProfiles.Where(p => p.UserId == id)
            .Select(p => p.AlbumNumber)
            .SingleOrDefaultAsync(cancellationToken);

        var facultyIds = await _db.StaffAssignments.Where(s => s.UserId == id)
            .Select(s => s.FacultyId)
            .ToListAsync(cancellationToken);

        return new AccountDetails(user.Id, user.FirstName, user.LastName, user.Email!,
            InOrder(roles), albumNumber, facultyIds, user.IsActive, user.MustChangePassword,
            user.CreatedAt, IsYou: user.Id == ActorId);
    }

    // Always in the same order as RoleNames.All, whatever order the database returns
    private static List<string> InOrder(IEnumerable<string> roles) =>
        RoleNames.All.Where(roles.Contains).ToList();

    // The same answer as a form rejected by [ApiController] - errors land under the fields
    private ActionResult FieldErrors(Dictionary<string, string> errors)
    {
        foreach (var (field, message) in errors)
            ModelState.AddModelError(field, message);

        return ValidationProblem(ModelState);
    }
}