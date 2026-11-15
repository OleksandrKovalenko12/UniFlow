using System.ComponentModel.DataAnnotations;

namespace UniFlow.Shared.Drafts;

public sealed record DraftFileInfo(Guid Id, string FileName, long SizeBytes);

// The draft opened in the form. FormData is the form's JSON as typed so far.
// RemovedAttachmentIds: correction only - the application's files to be archived.
public sealed record DraftDetails(
    Guid Id,
    string TypeCode,
    int EnrollmentId,
    string FormData,
    DateTime UpdatedAt,
    DateTime ExpiresAt,
    IReadOnlyList<DraftFileInfo> Files,
    IReadOnlyList<Guid> RemovedAttachmentIds);

// A card in "Niedokończone" on the student's list
public sealed record DraftSummary(
    Guid Id,
    string TypeCode,
    string TypeName,
    int EnrollmentId,
    string FieldOfStudyName,
    string StudyLevel,
    DateTime UpdatedAt,
    DateTime ExpiresAt,
    int FileCount);

public sealed class SaveDraftRequest
{
    // The same as Draft.MaxFormDataLength in Domain
    public const int MaxFormDataLength = 20_000;

    [Range(1, int.MaxValue)]
    public int EnrollmentId { get; set; }

    [Required]
    [MaxLength(MaxFormDataLength)]
    public string FormData { get; set; } = "";
}

// The changes to an application sent back for correction, saved as they are typed
public sealed class SaveCorrectionRequest
{
    [Required]
    [MaxLength(SaveDraftRequest.MaxFormDataLength)]
    public string FormData { get; set; } = "";

    // The student's files of the application to be archived when it is submitted again
    [MaxLength(20)]
    public List<Guid> RemovedAttachmentIds { get; set; } = [];
}