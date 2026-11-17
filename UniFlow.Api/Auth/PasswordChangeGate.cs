using UniFlow.Api.Errors;
using UniFlow.Shared.Auth;

namespace UniFlow.Api.Auth;

// With a temporary password only logging in, logging out and setting a new password work.
// The page sends the user to "Ustaw nowe hasło" by itself - this is the server's own check,
// so the API cannot be used around it.
public sealed class PasswordChangeGate
{
    private readonly RequestDelegate _next;

    public PasswordChangeGate(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        var path = context.Request.Path;

        if (context.User.HasClaim(c => c.Type == UniFlowClaims.MustChangePassword)
            && path.StartsWithSegments("/api")
            && !path.StartsWithSegments("/api/auth"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = StatusCodes.Status403Forbidden,
                    Extensions = { ["code"] = ErrorMapping.PasswordChangeRequired }
                }
            });
            return;
        }

        await _next(context);
    }
}