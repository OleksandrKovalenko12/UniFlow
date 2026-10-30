namespace UniFlow.Domain.Enums;

public enum DomainErrorCode
{
    // Workflow and permissions
    InvalidStatus = 1,
    RoleNotAllowed = 2,
    NotApplicant = 3,
    ConflictOfInterest = 4,

    // Submission
    NotOwnEnrollment = 5,
    EnrollmentInactive = 6,
    ApplicationTypeInactive = 7,

    // Decision path
    DeanDecisionRequired = 8,
    DeanDecisionNotRequired = 9,

    // Attachments
    AttachmentLimitReached = 10,
    AttachmentNotFound = 11,
    AttachmentNotArchivable = 12,
    AttachmentAlreadyArchived = 13,

    // Enrollment
    EnrollmentAlreadyInactive = 14,
    EnrollmentAlreadyActive = 15,
}