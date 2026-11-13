namespace UniFlow.Shared.Applications;

// One application on the student's own list
public sealed record MyApplicationItem(
    Guid Id,
    string Number,
    string TypeName,
    ApplicationState Status,
    DateTime SubmittedAt,
    string FieldOfStudyName,
    string StudyLevel);