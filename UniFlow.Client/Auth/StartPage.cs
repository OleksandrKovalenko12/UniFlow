using System.Security.Claims;
using UniFlow.Shared;

namespace UniFlow.Client.Auth;

// Where work begins after login. Staff first: a student who also works in the dean's office
// opens the applications to review; their own ones are a click away in the menu.
public static class StartPage
{
    // Null when the account has no role yet - there is nowhere to send it
    public static string? For(ClaimsPrincipal user) =>
        user.IsInRole(RoleNames.DeanOffice) || user.IsInRole(RoleNames.ViceDean) ? "review"
        : user.IsInRole(RoleNames.Student) ? "my"
        : user.IsInRole(RoleNames.Administrator) ? "admin"
        : null;
}