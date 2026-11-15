using UniFlow.Domain.Enums;

namespace UniFlow.Domain.Entities;

// An application the student has started but not submitted yet. It has no number and no
// status and is not part of the workflow - only a place for the form and its files.
// One per student and type; deleted after RetentionDays without changes (personal data).
public class Draft
{
    public const int RetentionDays = 30;
    public const int MaxFormDataLength = 20_000;

    private readonly List<DraftFile> _files = new();

    public Guid Id { get; private set; }
    public string StudentId { get; private set; } = null!;
    public int EnrollmentId { get; private set; }
    public Enrollment Enrollment { get; private set; } = null!;
    public int ApplicationTypeId { get; private set; }
    public ApplicationType ApplicationType { get; private set; } = null!;
    public string FormData { get; private set; } = null!;
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<DraftFile> Files => _files;

    public DateTime ExpiresAt => UpdatedAt.AddDays(RetentionDays);

    public Draft(string studentId, Enrollment enrollment, ApplicationType applicationType,
        string formData, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);
        ArgumentNullException.ThrowIfNull(applicationType);

        Id = Guid.CreateVersion7();
        StudentId = studentId;
        ApplicationType = applicationType;
        ApplicationTypeId = applicationType.Id;

        Update(enrollment, formData, now);
    }

    private Draft() { }

    // The form as typed so far - it does not have to be valid yet.
    // The studies may change too ("Zmień"), but only to the student's own active ones.
    public void Update(Enrollment enrollment, string formData, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        ArgumentException.ThrowIfNullOrWhiteSpace(formData);

        if (formData.Length > MaxFormDataLength)
            throw new ArgumentException($"Form data must be at most {MaxFormDataLength} characters.",
                nameof(formData));
        if (now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Time must be in UTC.", nameof(now));

        if (enrollment.StudentId != StudentId)
            throw new DomainException(DomainErrorCode.NotOwnEnrollment,
                "A draft can only use the student's own enrollment.");
        if (!enrollment.IsActive)
            throw new DomainException(DomainErrorCode.EnrollmentInactive, "Enrollment must be active.");

        Enrollment = enrollment;
        EnrollmentId = enrollment.Id;
        FormData = formData;
        UpdatedAt = now;
    }

    // Before the file is saved and converted - that takes seconds
    public void EnsureCanAddFile()
    {
        if (_files.Count >= ApplicationType.MaxAttachments)
            throw new DomainException(DomainErrorCode.AttachmentLimitReached,
                $"This application allows at most {ApplicationType.MaxAttachments} attachments.");
    }

    public DraftFile AddFile(StoredFile file, DateTime now)
    {
        EnsureCanAddFile();

        var draftFile = new DraftFile(file, now);
        _files.Add(draftFile);
        UpdatedAt = now;

        return draftFile;
    }

    // Returned so its copy on disk can be deleted: nothing was submitted, nothing to keep
    public DraftFile RemoveFile(Guid fileId, DateTime now)
    {
        var draftFile = _files.SingleOrDefault(f => f.Id == fileId)
            ?? throw new DomainException(DomainErrorCode.AttachmentNotFound,
                "File not found in this draft.");

        _files.Remove(draftFile);
        UpdatedAt = now;

        return draftFile;
    }
}