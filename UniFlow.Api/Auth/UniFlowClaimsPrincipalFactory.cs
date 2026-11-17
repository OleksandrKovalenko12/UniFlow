using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared.Auth;

namespace UniFlow.Api.Auth;

// Builds what goes into the login cookie: the standard claims (id, name, roles) plus
// a mark of a temporary password. It runs at login and again at every security stamp check,
// so the mark disappears once the user has set their own password.
public sealed class UniFlowClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public UniFlowClaimsPrincipalFactory(UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.MustChangePassword)
            identity.AddClaim(new Claim(UniFlowClaims.MustChangePassword, "true"));

        return identity;
    }
}