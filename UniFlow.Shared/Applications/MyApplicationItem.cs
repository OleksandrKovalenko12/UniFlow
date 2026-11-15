namespace UniFlow.Shared.Applications;

// One application on the student's own list. InProgress: still moving through the workflow -
// no second application of this type on these studies until it is decided or withdrawn.
public sealed record MyApplicationItem(
    Guid Id,
    string Number,
    string TypeCode,
    string TypeName,
    ApplicationState Status,
    bool InProgress,
    DateTime SubmittedAt,
    int EnrollmentId,
    string FieldOfStudyName,
    string StudyLevel);