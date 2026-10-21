using UniFlow.Domain.Enums;

namespace UniFlow.Domain.Entities;

public class StatusHistoryEntry
{
    private const int MaxCommentLength = 2000;
    
    public long Id { get; private set; }
    public Guid ApplicationId { get; private set;  }
    public ApplicationStatus? FromStatus { get; private set; }
    public ApplicationStatus ToStatus { get; private set; }
    public string ChangedById { get; private set; } = null!;
    public DateTime ChangedAt { get; private set; }
    public string? Comment { get; private set; }
    public bool IsInternal { get; private set; }

    internal StatusHistoryEntry(ApplicationStatus? fromStatus, ApplicationStatus toStatus,
        string changedById, DateTime changedAt, string? comment, bool isInternal)
    {
        if (string.IsNullOrWhiteSpace(changedById))
            throw new ArgumentException("ChangedById is required", nameof(changedById));
        if (changedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Time must be in UTC.", nameof(changedAt));

        var normalizedComment = NormalizeComment(comment);

        if (isInternal && normalizedComment is null)
            throw new ArgumentException("If internal is 'true', comment is required",
                nameof(comment));

        FromStatus = fromStatus; 
        ToStatus = toStatus;
        ChangedById = changedById;
        ChangedAt = changedAt;
        Comment = normalizedComment;
        IsInternal = isInternal;
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return null;

        var trimmed = comment.Trim();

        if (trimmed.Length > MaxCommentLength)
            throw new ArgumentException($"Comment must be at most {MaxCommentLength} characters long.",
                nameof(comment));

        return trimmed;
    }

    private StatusHistoryEntry() { }
}
