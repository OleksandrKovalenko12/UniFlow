using UniFlow.Shared.Enrollments;
using UniFlow.Shared.Review;

namespace UniFlow.Shared.Applications;

// One application as its author or an employee of its faculty sees it.
// FormData is the form's JSON - the page reads it with the form class of TypeCode.
// ForApplicant: the author's view (a student working in the dean's office is both).
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
    bool CanWithdraw);


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