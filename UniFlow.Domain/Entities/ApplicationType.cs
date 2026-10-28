using System.Text.RegularExpressions;

namespace UniFlow.Domain.Entities;

public class ApplicationType
{
    private const int MaxAllowedAttachments = 20;
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 1000;

    public int Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool RequiresDeanDecision {  get; private set; }
    public int MaxAttachments { get; private set; }
    public bool IsActive { get; private set;  }

    public ApplicationType(string code, string name, string description, bool requiresDeanDecision,
        int maxAttachments)
    {
        MaxAttachments = ValidateMaxAttachments(maxAttachments, nameof(maxAttachments));
        Code = ValidateCode(code);
        Name = NormalizeText(name, nameof(name),MaxNameLength);
        Description = NormalizeText(description, nameof(description),MaxDescriptionLength);
        RequiresDeanDecision = requiresDeanDecision;
        IsActive = true;
    }

    public void Rename(string newName)
    {
        Name = NormalizeText(newName, nameof(newName), MaxNameLength);
    }

    public void ChangeDescription(string newDescription)
    {
        Description = NormalizeText(newDescription, nameof(newDescription), MaxDescriptionLength);
    }
    public void SetMaxAttachments(int maxAttachments)
    {
        MaxAttachments = ValidateMaxAttachments(maxAttachments, nameof(maxAttachments));
    }

    public void SetRequiresDeanDecision(bool value)
    {
        RequiresDeanDecision = value;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private ApplicationType() { }

    private static string ValidateCode(string code)
    {
        if (string.IsNullOrEmpty(code) || !Regex.IsMatch(code, "^[A-Z][A-Z0-9_]*$"))
            throw new ArgumentException("Code must start with an uppercase letter and contain only A-Z, 0-9 and '_'.", nameof(code));
        return code;
    }

    private static string NormalizeText(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException($"{paramName} must be at most {maxLength} characters long.", paramName);

        return normalized;
    }

    private static int ValidateMaxAttachments(int value, string paramName)
    {
        if (value < 0 || value > MaxAllowedAttachments)
            throw new ArgumentOutOfRangeException(paramName,
                $"Max attachments must be between 0 and {MaxAllowedAttachments}.");

        return value;
    }
}
