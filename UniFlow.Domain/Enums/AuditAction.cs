namespace UniFlow.Domain.Enums;

public enum AuditAction
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
}
