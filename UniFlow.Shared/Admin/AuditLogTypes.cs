using System.Text.Json.Serialization;

namespace UniFlow.Shared.Admin;

// What happened, as the browser sees it. Same names and numbers as AuditAction in Domain
// (the browser does not reference Domain) - a test keeps the two in step.
[JsonConverter(typeof(JsonStringEnumConverter<AuditEntryKind>))]
public enum AuditEntryKind
{
    UserActivated = 1,
    UserDeactivated = 2,
    LoginEmailChanged = 3,
    RoleAssigned = 4,
    RoleRemoved = 5,
    StaffAssigned = 6,
    StaffUnassigned = 7,
    PersonalDataCorrected = 8,
    FacultyCreated = 9,
    FacultyChanged = 10,
    FieldOfStudyCreated = 11,
    FieldOfStudyChanged = 12,
    ApplicationTypeChanged = 13,
    UserCreated = 14,
    PasswordReset = 15,
}

// The "Rodzaj czynności" filter - kinds of entries put together the way an administrator looks
// for them
public enum AuditGroup
{
    Accounts,
    Roles,
    Faculties,
    Structure
}

public static class AuditGroups
{
    public static IReadOnlyList<AuditEntryKind> KindsOf(AuditGroup group) => group switch
    {
        AuditGroup.Accounts =>
        [
            AuditEntryKind.UserCreated, AuditEntryKind.UserActivated, AuditEntryKind.UserDeactivated,
            AuditEntryKind.PasswordReset, AuditEntryKind.LoginEmailChanged,
            AuditEntryKind.PersonalDataCorrected
        ],
        AuditGroup.Roles => [AuditEntryKind.RoleAssigned, AuditEntryKind.RoleRemoved],
        AuditGroup.Faculties => [AuditEntryKind.StaffAssigned, AuditEntryKind.StaffUnassigned],
        AuditGroup.Structure =>
        [
            AuditEntryKind.FacultyCreated, AuditEntryKind.FacultyChanged,
            AuditEntryKind.FieldOfStudyCreated, AuditEntryKind.FieldOfStudyChanged,
            AuditEntryKind.ApplicationTypeChanged
        ],
        _ => []
    };
}

// One entry of the audit log. Target: the account it is about - none for faculties, fields of
// study and application types. Details: a role name, a faculty code or "old → new".
public sealed record AuditLogItem(
    long Id,
    DateTime CreatedAt,
    string ActorName,
    AuditEntryKind Kind,
    string? TargetUserId,
    string? TargetName,
    string? Details);