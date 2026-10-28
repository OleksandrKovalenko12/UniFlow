using UniFlow.Domain.Enums;

namespace UniFlow.Domain.Entities;

public class AuditLogEntry
{
    private const int MaxDetailsLength = 2000;
    private const int MaxUserIdLength = 450;

    public long Id { get; private set; }
    public string ActorId { get; private set; } = null!;
    public AuditAction Action { get; private set; }
    public string? TargetUserId { get; private set; }
    public string? Details { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public AuditLogEntry(string actorId, AuditAction action, string? targetUserId, 
        string? details, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(nameof(action), "Unknown audit action.");
        if (createdAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Time must be in UTC.", nameof(createdAt));

        ActorId = actorId;
        Action = action;
        TargetUserId = NormalizeOptional(targetUserId, MaxUserIdLength,
            nameof(targetUserId));
        Details = NormalizeOptional(details, MaxDetailsLength, nameof(details));
        CreatedAt = createdAt;
    }

    private AuditLogEntry() { }

    private static string? NormalizeOptional(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
            throw new ArgumentException($"Value must be at most {maxLength} characters long.",
                paramName);

        return trimmed;
    }
}
