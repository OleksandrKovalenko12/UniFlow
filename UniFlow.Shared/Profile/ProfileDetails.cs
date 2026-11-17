using UniFlow.Shared.Enrollments;

namespace UniFlow.Shared.Profile;

// The logged-in user's own data, read only - it comes from the university's system.
// Roles: the RoleNames codes; AlbumNumber and Studies only for a student, Faculties only
// for staff (one person can be both).
public sealed record ProfileDetails(
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    string? AlbumNumber,
    IReadOnlyList<EnrollmentSummary> Studies,
    IReadOnlyList<string> Faculties);