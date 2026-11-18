using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniFlow.Shared.Admin;

// The level of studies as the browser sees it. Same names and numbers as StudyLevel in Domain
// (the browser does not reference Domain) - a test keeps the two in step.
[JsonConverter(typeof(JsonStringEnumConverter<StudyLevelOption>))]
public enum StudyLevelOption
{
    FirstCycle = 1,
    SecondCycle = 2,
    LongCycle = 3
}

// A faculty with its fields of study - one card on the "Struktura uczelni" page
public sealed record FacultyItem(
    int Id,
    string Code,
    string Name,
    bool IsActive,
    IReadOnlyList<FieldOfStudyItem> FieldsOfStudy);

// LevelText: "studia pierwszego stopnia" - the same words as everywhere else
public sealed record FieldOfStudyItem(
    int Id,
    int FacultyId,
    string Name,
    StudyLevelOption Level,
    string LevelText,
    bool IsActive);

// A new or renamed faculty. The same rules as in Faculty (Domain).
public sealed class FacultyForm : IValidatableObject
{
    public const int MinCodeLength = 2;
    public const int MaxCodeLength = 8;
    public const int MaxNameLength = 200;

    // Short, e.g. "WIiT" - shown in lists next to field names
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    // Editing only - a new faculty is always switched on
    public bool IsActive { get; set; } = true;

    public void Normalize()
    {
        Code = Code?.Trim() ?? "";
        Name = Name?.Trim() ?? "";
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var code = Code?.Trim() ?? "";
        if (code.Length == 0)
            yield return new ValidationResult("Podaj kod wydziału.", [nameof(Code)]);
        else if (code.Length < MinCodeLength || code.Length > MaxCodeLength || !code.All(char.IsLetterOrDigit))
            yield return new ValidationResult($"Kod to {MinCodeLength}-{MaxCodeLength} liter lub cyfr.",
                [nameof(Code)]);

        if (string.IsNullOrWhiteSpace(Name))
            yield return new ValidationResult("Podaj nazwę wydziału.", [nameof(Name)]);
        else if (Name.Trim().Length > MaxNameLength)
            yield return new ValidationResult($"Nazwa może mieć najwyżej {MaxNameLength} znaków.", [nameof(Name)]);
    }
}

// A new field of study, or a renamed / moved / switched off one. The level is chosen once -
// the number of semesters of everyone's studies depends on it - so editing ignores it.
public sealed class FieldOfStudyForm : IValidatableObject
{
    public const int MaxNameLength = 200;

    public string Name { get; set; } = "";
    public StudyLevelOption Level { get; set; }
    public int FacultyId { get; set; }

    // Editing only - a new field of study is always switched on
    public bool IsActive { get; set; } = true;

    public void Normalize() => Name = Name?.Trim() ?? "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
            yield return new ValidationResult("Podaj nazwę kierunku.", [nameof(Name)]);
        else if (Name.Trim().Length > MaxNameLength)
            yield return new ValidationResult($"Nazwa może mieć najwyżej {MaxNameLength} znaków.", [nameof(Name)]);

        if (!Enum.IsDefined(Level))
            yield return new ValidationResult("Wybierz poziom studiów.", [nameof(Level)]);

        if (FacultyId <= 0)
            yield return new ValidationResult("Wybierz wydział.", [nameof(FacultyId)]);
    }
}