using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace UniFlow.Shared.Forms;

// A dean's leave: what kind, for which part of which academic year, and why. Only the current
// and the next academic year can be asked for - only the server knows the date for sure, so
// ValidateAgainst checks it. A health or parental leave needs a document (RequiredAttachments).
public sealed partial class DeanLeaveForm : IValidatableObject
{
    public const int MaxJustificationLength = 2000;

    public DeanLeaveKind Kind { get; set; }
    public DeanLeavePeriod Period { get; set; }

    // "2026/2027"
    public string? AcademicYear { get; set; }

    public string Justification { get; set; } = "";

    // Medical records, a birth certificate - the occasional leave may go without.
    // Not saved with the form - it follows from it.
    [JsonIgnore]
    public int RequiredAttachments => Kind is DeanLeaveKind.Health or DeanLeaveKind.Parental ? 1 : 0;

    public void Normalize()
    {
        AcademicYear = AcademicYear?.Trim();
        Justification = Justification?.Trim() ?? "";
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Kind))
            yield return Error("Wybierz rodzaj urlopu.", nameof(Kind));

        if (!Enum.IsDefined(Period))
            yield return Error("Wybierz okres urlopu.", nameof(Period));

        if (string.IsNullOrWhiteSpace(AcademicYear))
            yield return Error("Wybierz rok akademicki.", nameof(AcademicYear));
        else if (StartYear(AcademicYear) is null)
            yield return Error("Podaj rok akademicki w formacie 2026/2027.", nameof(AcademicYear));

        if (string.IsNullOrWhiteSpace(Justification))
            yield return Error("Podaj uzasadnienie.", nameof(Justification));
        else if (Justification.Trim().Length > MaxJustificationLength)
            yield return Error($"Uzasadnienie może mieć najwyżej {MaxJustificationLength} znaków.",
                nameof(Justification));
    }

    // Not back in time, not years ahead: the current or the next academic year
    public IEnumerable<ValidationResult> ValidateAgainst(int currentStartYear)
    {
        var startYear = StartYear(AcademicYear);
        if (startYear is not null && startYear != currentStartYear && startYear != currentStartYear + 1)
            yield return Error("Urlop można wziąć na bieżący albo na następny rok akademicki.",
                nameof(AcademicYear));
    }

    // "2026/2027" -> 2026; null when it is not two years one after another
    public static int? StartYear(string? academicYear)
    {
        var match = YearFormat().Match(academicYear?.Trim() ?? "");
        if (!match.Success)
            return null;

        var first = int.Parse(match.Groups[1].Value);
        var second = int.Parse(match.Groups[2].Value);
        return second == first + 1 ? first : null;
    }

    [GeneratedRegex(@"^(\d{4})/(\d{4})$")]
    private static partial Regex YearFormat();

    private static ValidationResult Error(string message, string memberName) =>
        new(message, [memberName]);
}