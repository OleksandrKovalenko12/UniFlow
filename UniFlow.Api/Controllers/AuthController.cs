using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared.Auth;

namespace UniFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    // One message for every failure - does not reveal which e-mails exist
    private const string LoginFailedMessage = "Nieprawidłowy e-mail lub hasło.";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager, ILogger<AuthController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUser>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            _logger.LogWarning("Login failed: unknown e-mail.");
            return LoginFailed();
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed for user {UserId}: account is inactive.", user.Id);
            return LoginFailed();
        }

        var result = await _signInManager.PasswordSignInAsync(user, request.Password,
            isPersistent: false, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Login failed for user {UserId}: {Reason}.", user.Id,
                result.IsLockedOut ? "account is locked out" : "wrong password");
            return LoginFailed();
        }

        _logger.LogInformation("User {UserId} logged in.", user.Id);
        return await ToCurrentUserAsync(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    // Replaces the temporary password with the user's own. Not limited to temporary passwords -
    // the current one is asked for anyway.
    [HttpPost("password")]
    public async Task<ActionResult<CurrentUser>> ChangePassword(ChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
            return Unauthorized();

        // Saved together with the new password - ChangePasswordAsync saves the whole user
        user.MustChangePassword = false;

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            // A wrong current password under its field, the password rules under the new one
            foreach (var error in result.Errors)
                ModelState.AddModelError(
                    error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)
                        ? nameof(request.CurrentPassword)
                        : nameof(request.NewPassword),
                    error.Description);

            return ValidationProblem(ModelState);
        }

        // A new cookie: without the temporary password mark and with the new security stamp
        await _signInManager.RefreshSignInAsync(user);
        _logger.LogInformation("User {UserId} changed their password.", user.Id);

        return await ToCurrentUserAsync(user);
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUser>> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
            return Unauthorized();

        return await ToCurrentUserAsync(user);
    }

    private ObjectResult LoginFailed() =>
        Problem(title: LoginFailedMessage, statusCode: StatusCodes.Status401Unauthorized);

    private async Task<CurrentUser> ToCurrentUserAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new CurrentUser(user.Id, user.Email!, user.FirstName, user.LastName, [.. roles],
             user.MustChangePassword);
    }
}