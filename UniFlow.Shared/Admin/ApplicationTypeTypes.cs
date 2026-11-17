using System.ComponentModel.DataAnnotations;

namespace UniFlow.Shared.Admin;

// A kind of application as the administrator sees it. New kinds are not created here - each needs
// its own form and PDF template, which is a programmer's job.
public sealed record ApplicationTypeDetails(
    int Id,
    string Code,
    string Name,
    string Description,
    bool RequiresDeanDecision,
    int MaxAttachments,
    bool IsActive);

// What the administrator may change. The same limits as in ApplicationType (Domain).
public sealed class ApplicationTypeForm : IValidatableObject
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 1000;
    public const int MaxAttachmentsLimit = 20;

    public string Name { get; set; } = "";

    // Shown to the student when choosing the kind
    public string Description { get; set; } = "";

    public bool RequiresDeanDecision { get; set; }

    public int MaxAttachments { get; set; }

    public bool IsActive { get; set; }

    public static ApplicationTypeForm From(ApplicationTypeDetails type) => new()
    {
        Name = type.Name,
        Description = type.Description,
        RequiresDeanDecision = type.RequiresDeanDecision,
        MaxAttachments = type.MaxAttachments,
        IsActive = type.IsActive,
    };

    public void Normalize()
    {
        Name = Name?.Trim() ?? "";
        Description = Description?.Trim() ?? "";
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
            yield return new ValidationResult("Podaj nazwę.", [nameof(Name)]);
        else if (Name.Trim().Length > MaxNameLength)
            yield return new ValidationResult($"Nazwa może mieć najwyżej {MaxNameLength} znaków.", [nameof(Name)]);

        if (string.IsNullOrWhiteSpace(Description))
            yield return new ValidationResult("Podaj opis dla studenta.", [nameof(Description)]);
        else if (Description.Trim().Length > MaxDescriptionLength)
            yield return new ValidationResult($"Opis może mieć najwyżej {MaxDescriptionLength} znaków.",
                [nameof(Description)]);

        if (MaxAttachments < 0 || MaxAttachments > MaxAttachmentsLimit)
            yield return new ValidationResult($"Limit załączników to od 0 do {MaxAttachmentsLimit}.",
                [nameof(MaxAttachments)]);
    }
}