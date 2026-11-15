using UniFlow.Domain.Enums;

namespace UniFlow.Domain.Entities;

// An application the student has started but not submitted yet - or the changes to an
// application sent back for correction (ApplicationId set). It has no number and no status and
// is not part of the workflow: the dean's office sees nothing until the student submits.
// One per student and type (new application) or per application (correction); deleted after
// RetentionDays without changes (personal data).
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

    // Correction only: the application being corrected and the student's files of it that are
    // to be archived when it is submitted again
    public Guid? ApplicationId { get; private set; }
    public Application? Application { get; private set; }
    public Guid[] RemovedAttachmentIds { get; private set; } = [];

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

    // The changes to an application sent back to its author. The studies stay those of the
    // application - it already has a number.
    public static Draft ForCorrection(Application application, string formData, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(application);
        EnsureCanBeCorrected(application);

        return new Draft(application.ApplicantId, application.Enrollment, application.ApplicationType,
            formData, now)
        {
            ApplicationId = application.Id,
            Application = application
        };
    }

    // The form as typed so far - it does not have to be valid yet.
    // The studies may change too ("Zmień"), but only to the student's own active ones.
    public void Update(Enrollment enrollment, string formData, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(enrollment);

        if (enrollment.StudentId != StudentId)
            throw new DomainException(DomainErrorCode.NotOwnEnrollment,
                "A draft can only use the student's own enrollment.");
        if (!enrollment.IsActive)
            throw new DomainException(DomainErrorCode.EnrollmentInactive, "Enrollment must be active.");

        SetFormData(formData, now);
        Enrollment = enrollment;
        EnrollmentId = enrollment.Id;
    }

    // Correction: the form and the files to remove. Only the student's own files still in the
    // application can be removed; keeping one again needs room left by the files added since.
    public void UpdateCorrection(string formData, IEnumerable<Guid> removedAttachmentIds, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(removedAttachmentIds);
        var application = Application
            ?? throw new InvalidOperationException("Not a correction draft or its application is not loaded.");
        EnsureCanBeCorrected(application);

        var removed = removedAttachmentIds.Distinct().ToArray();
        if (removed.Any(id => !StudentFilesOf(application).Any(a => a.Id == id)))
            throw new DomainException(DomainErrorCode.AttachmentNotFound,
                "Only the student's files still in the application can be removed.");

        var kept = StudentFilesOf(application).Count(a => !removed.Contains(a.Id));
        if (kept + _files.Count > application.MaxAttachments)
            throw new DomainException(DomainErrorCode.AttachmentLimitReached,
                $"This application allows at most {application.MaxAttachments} attachments.");

        SetFormData(formData, now);
        RemovedAttachmentIds = removed;
    }

    // Before the file is saved and converted - that takes seconds
    public void EnsureCanAddFile()
    {
        if (_files.Count >= MaxFiles)
            throw new DomainException(DomainErrorCode.AttachmentLimitReached,
                $"This application allows at most {MaxFiles} new attachments.");
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

    // How many files the draft may hold: the type's limit for a new application; for a
    // correction the application's own limit minus the files it keeps
    private int MaxFiles
    {
        get
        {
            if (ApplicationId is null)
                return ApplicationType.MaxAttachments;

            var application = Application
                ?? throw new InvalidOperationException("The application of a correction draft is not loaded.");
            return application.MaxAttachments
                - StudentFilesOf(application).Count(a => !RemovedAttachmentIds.Contains(a.Id));
        }
    }

    private void SetFormData(string formData, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formData);

        if (formData.Length > MaxFormDataLength)
            throw new ArgumentException($"Form data must be at most {MaxFormDataLength} characters.",
                nameof(formData));
        if (now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Time must be in UTC.", nameof(now));

        FormData = formData;
        UpdatedAt = now;
    }

    private static void EnsureCanBeCorrected(Application application)
    {
        if (application.Status != ApplicationStatus.NeedsCorrection)
            throw new DomainException(DomainErrorCode.InvalidStatus,
                "Only an application sent back for correction can be corrected.");
    }

    private static IEnumerable<Attachment> StudentFilesOf(Application application) =>
        application.Attachments.Where(a => a.Kind == AttachmentKind.StudentUpload && !a.IsArchived);
}