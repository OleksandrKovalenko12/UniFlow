namespace UniFlow.Shared.Auth;

// Claims of our own, next to the standard ones (id, name, e-mail, roles)
public static class UniFlowClaims
{
    // The user logged in with a temporary password and has to set their own first
    public const string MustChangePassword = "uniflow:must_change_password";
}