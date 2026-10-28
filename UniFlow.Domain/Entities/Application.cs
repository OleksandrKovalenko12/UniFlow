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
    public int ApplicationTypeId { get; private set; }
    public ApplicationType ApplicationType { get; private set; } = null!;
    public ApplicationStatus Status { get; private set; }
    public bool RequiresDeanDecision { get; private set; }
    public int MaxAttachments { get; private set; }
    public string FormData { get; private set; } = null!;
    public string? AssignedToId { get; private set; }
    public DateTime CreatedAt { get; private set; }
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
        MaxAttachments = applicationType.MaxAttachments;
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

    //State machine
    public void TakeForReview(Actor actor, DateTime now)
    {
        EnsureStatus(ApplicationStatus.Submitted, ApplicationStatus.Resubmitted);
        EnsureRole(actor, ActorRole.DeanOffice);

        AssignedToId = actor.Id;

        ChangeStatus(ApplicationStatus.UnderReview, actor, now, comment: null, isInternal: false);
    }

    public void Approve(Actor actor, DateTime now)
    {
        if (Status == ApplicationStatus.UnderReview)
        {
            EnsureRole(actor, ActorRole.DeanOffice);
            if (RequiresDeanDecision)
                throw new InvalidOperationException("This application must be decided by the vice-dean.");
        }
        else if (Status == ApplicationStatus.ForwardedToDean)
        {
            EnsureRole(actor, ActorRole.ViceDean);
        }
        else
        {
            throw new InvalidOperationException($"Cannot approve an application in status {Status}.");
        }

        ChangeStatus(ApplicationStatus.Approved, actor, now, comment: null, isInternal: false);
    }

    public void RequestCorrection(Actor actor, string comment, DateTime now)
    {
        RequireComment(comment);
        EnsureStatus(ApplicationStatus.UnderReview);
        EnsureRole(actor, ActorRole.DeanOffice);

        ChangeStatus(ApplicationStatus.NeedsCorrection, actor, now, comment, isInternal: false);
    }

    public void Resubmit(Actor actor, string newFormData, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newFormData);

        EnsureStatus(ApplicationStatus.NeedsCorrection);
        EnsureApplicant(actor);

        FormData = newFormData;

        ChangeStatus(ApplicationStatus.Resubmitted, actor, now, comment: null, isInternal: false);
    }

    public void ForwardToDean(Actor actor, string? internalNote, DateTime now)
    {
        EnsureStatus(ApplicationStatus.UnderReview);
        EnsureRole(actor, ActorRole.DeanOffice);

        if (!RequiresDeanDecision)
            throw new InvalidOperationException("This application is decided by the dean's " +
                "office and cannot be forwarded.");

        ChangeStatus(ApplicationStatus.ForwardedToDean, actor, now, comment: internalNote,
            isInternal: !string.IsNullOrWhiteSpace(internalNote));
    }

    public void Reject(Actor actor, string comment, DateTime now)
    {
        RequireComment(comment);

        if (Status == ApplicationStatus.UnderReview ||
            Status == ApplicationStatus.NeedsCorrection)
        {
            EnsureRole(actor, ActorRole.DeanOffice);
        }
        else if (Status == ApplicationStatus.ForwardedToDean)
        {
            EnsureRole(actor, ActorRole.ViceDean);
        }
        else
        {
            throw new InvalidOperationException($"Cannot reject an application in status {Status}.");
        }

        ChangeStatus(ApplicationStatus.Rejected, actor, now, comment, isInternal: false);
    }

    public void ReturnToDeanOffice(Actor actor, string internalComment, DateTime now)
    {
        RequireComment(internalComment);
        EnsureStatus(ApplicationStatus.ForwardedToDean);
        EnsureRole(actor, ActorRole.ViceDean);

        ChangeStatus(ApplicationStatus.UnderReview, actor, now, internalComment, isInternal: true);
    }

    public void Withdraw(Actor actor, DateTime now)
    {
        EnsureStatus(ApplicationStatus.Submitted, ApplicationStatus.UnderReview,
            ApplicationStatus.NeedsCorrection, ApplicationStatus.Resubmitted);
        EnsureApplicant(actor);

        ChangeStatus(ApplicationStatus.Withdrawn, actor, now, comment: null, isInternal: false);
    }

    //Validation and helpers
    private void EnsureStatus(params ApplicationStatus[] allowed)
    {
        if (!allowed.Contains(Status))
            throw new InvalidOperationException($"Operation not allowed in status {Status}. Allowed: " +
                $"{string.Join(", ", allowed)}");
    }

    private void EnsureRole(Actor actor, ActorRole required)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (actor.Role != required)
            throw new InvalidOperationException($"Only {required} can perform this operation.");
        if (required != ActorRole.Student && actor.Id == ApplicantId)     
            throw new InvalidOperationException("Staff members cannot process their own applications.");
    }

    private void EnsureApplicant(Actor actor)
    {
        EnsureRole(actor, ActorRole.Student);

        if (actor.Id != ApplicantId)
            throw new InvalidOperationException("Only the applicant can perform this operation.");
    }

    private static void RequireComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("A comment is required for this operation. ", nameof(comment));
    }

    private void ChangeStatus(ApplicationStatus newStatus, Actor actor, DateTime now,
        string? comment, bool isInternal)
    {
        _statusHistory.Add(new StatusHistoryEntry(
            fromStatus: Status,
            toStatus: newStatus,
            changedById: actor.Id,
            changedAt: now,
            comment: comment,
            isInternal: isInternal));

        Status = newStatus;
        UpdatedAt = now;
    }
}
