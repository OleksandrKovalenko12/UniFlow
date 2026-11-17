using UniFlow.Shared.Enrollments;
using UniFlow.Shared.Review;

namespace UniFlow.Shared.Applications;

// One application as its author or an employee of its faculty sees it.
// FormData is the form's JSON - the page reads it with the form class of TypeCode.
// ForApplicant: the author's view (a student working in the dean's office is both).
// StudentEmail and OtherApplications: only for staff - to write to the student and to see
// what else they have asked for; empty in the author's view.
public sealed record ApplicationDetails(
    Guid Id,
    string Number,
    string TypeCode,
    string TypeName,
    ApplicationState Status,
    DateTime SubmittedAt,
    bool RequiresDeanDecision,
    string StudentName,
    string AlbumNumber,
    EnrollmentSummary Studies,
    string? AssignedToName,
    string FormData,
    IReadOnlyList<AttachmentInfo> Attachments,
    IReadOnlyList<StatusChange> History,
    IReadOnlyList<ReviewAction> Actions,
        bool ForApplicant,
    bool CanWithdraw,
    string? StudentEmail,
    IReadOnlyList<RelatedApplication> OtherApplications);

// Another application of the same student - only one the employee could open anyway.
// InProgress: decided on the server, like on the student's own list.
public sealed record RelatedApplication(
    Guid Id,
    string Number,
    string TypeName,
    ApplicationState Status,
    bool InProgress,
    DateTime SubmittedAt);


// IsDocument: the application's own PDF (older versions too) rather than a student's file
public sealed record AttachmentInfo(
    Guid Id,
    string FileName,
    long SizeBytes,
    bool IsDocument,
    DateTime UploadedAt,
    DateTime? ArchivedAt);

// ChangedBy: a name for staff; for the student "Dziekanat" or "Prodziekan" instead of names
public sealed record StatusChange(
    ApplicationState? From,
    ApplicationState To,
    string ChangedBy,
    DateTime ChangedAt,
    string? Comment,
    bool IsInternal);