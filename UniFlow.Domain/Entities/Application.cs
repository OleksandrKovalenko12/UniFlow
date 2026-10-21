using UniFlow.Domain.Enums;

namespace UniFlow.Domain.Entities;

public class Application
{
    private readonly List<StatusHistoryEntry> _statusHistory = new();

    public Guid Id { get; private set; }
    public string Number { get; private set; } = null!;
    public string ApplicantId { get; private set; } = null!;
    public int EnrollmentId { get; private set; }
    public Enrollment Enrollment { get; private set; } = null!;
    public int ApplicationTypeId { get; private set;  }
    public ApplicationType ApplicationType { get; private set; } = null!;
    public ApplicationStatus Status { get; private set; }
    public bool RequiresDeanDecision { get; private set; }
    public string FormData { get; private set; } = null!;
    public string? AssignedToId { get; private set; }
    public DateTime CreatedAt { get; private set;  }
    public DateTime UpdatedAt { get; private set; }
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<StatusHistoryEntry> StatusHistory => _statusHistory;

    public Application(string number, string applicantId, Enrollment enrollment,
        ApplicationType applicationType, string formData, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(formData);

        ArgumentNullException.ThrowIfNull(enrollment);
        ArgumentNullException.ThrowIfNull(applicationType);

        if (enrollment.StudentId != applicantId)
            throw new InvalidOperationException("A student can submit applications " +
                "only within their own enrollment.");

        if (!enrollment.IsActive)
            throw new InvalidOperationException("Enrollment must be active.");
        if (!applicationType.IsActive)
            throw new InvalidOperationException("Application type must be active.");

        Id = Guid.CreateVersion7();

        Number = number;
        ApplicantId = applicantId;
        FormData = formData;
        Enrollment = enrollment;
        EnrollmentId = enrollment.Id;
        ApplicationType = applicationType;
        ApplicationTypeId = applicationType.Id;

        RequiresDeanDecision = applicationType.RequiresDeanDecision;
        Status = ApplicationStatus.Submitted;
        CreatedAt = now;
        UpdatedAt = now;

        _statusHistory.Add(new StatusHistoryEntry(
            fromStatus: null,
            toStatus: ApplicationStatus.Submitted,
            changedById: applicantId,
            changedAt: now,
            comment: null,
            isInternal: false));
    }

    private Application() { }
}
