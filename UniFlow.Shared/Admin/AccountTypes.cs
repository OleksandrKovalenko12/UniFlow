namespace UniFlow.Shared.Admin;

// One row of the account list
public sealed record AccountListItem(
    string Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    string? AlbumNumber,
    bool IsActive);

// One account as the administrator edits it.
// MustChangePassword: the user has not set their own password yet.
// IsYou: the administrator's own account - some changes are blocked on it.
public sealed record AccountDetails(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    IReadOnlyList<string> Roles,
    string? AlbumNumber,
    IReadOnlyList<int> FacultyIds,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt,
    bool IsYou);

// A temporary password, shown to the administrator once - it is not stored anywhere readable
public sealed record IssuedPassword(string AccountId, string Password);

// A faculty to choose for an employee
public sealed record FacultyOption(int Id, string Code, string Name);