namespace UniFlow.Shared.Auth;

public sealed record CurrentUser(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles);