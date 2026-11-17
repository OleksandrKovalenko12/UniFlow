using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Enrollments;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared;
using UniFlow.Shared.Enrollments;
using UniFlow.Shared.Profile;

namespace UniFlow.Api.Controllers;

// "Mój profil": the logged-in user's own data - the id comes from the cookie, so there is no way
// to ask for someone else's
[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    private readonly UniFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(UniFlowDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ProfileDetails>> Get(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);

        // E.g. the account was deleted while the cookie was still valid
        if (user is null)
            return Unauthorized();

        // The roles the user is logged in with - the same the permissions are checked against
        var roles = RoleNames.All.Where(User.IsInRole).ToList();

        var albumNumber = await _db.StudentProfiles
            .Where(p => p.UserId == user.Id)
            .Select(p => p.AlbumNumber)
            .SingleOrDefaultAsync(cancellationToken);

        IReadOnlyList<EnrollmentSummary> studies = User.IsInRole(RoleNames.Student)
            ? await EnrollmentSummaries.ActiveOfAsync(_db, user.Id, cancellationToken)
            : [];

        var faculties = await _db.StaffAssignments
            .Where(s => s.UserId == user.Id)
            .Select(s => s.Faculty.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        return new ProfileDetails($"{user.FirstName} {user.LastName}", user.Email!, roles,
            albumNumber, studies, faculties);
    }
}