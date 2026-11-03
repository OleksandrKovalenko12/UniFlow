using System.ComponentModel.DataAnnotations;

namespace UniFlow.Shared.Forms;

public sealed class GroupChangeForm : IValidatableObject
{
    public const int MaxGroupLength = 20;
    public const int MaxSubjectLength = 150;
    public const int MaxSubjects = 10;
    public const int MaxJustificationLength = 2000;

    public GroupChangeScope Scope { get; set; }

    // Scope = General
    public GroupType GroupType { get; set; }
    public string? CurrentGroup { get; set; }
    public string? RequestedGroup { get; set; }

    // Scope = Subjects
    public List<SubjectGroupChange> Subjects { get; set; } = [];

    public string Justification { get; set; } = "";

    // Trims text and clears the part of the form that the chosen scope does not use
    public void Normalize()
    {
        Justification = Justification?.Trim() ?? "";

        if (Scope == GroupChangeScope.General)
        {
            CurrentGroup = CurrentGroup?.Trim();
            RequestedGroup = RequestedGroup?.Trim();
            Subjects = [];
        }
        else if (Scope == GroupChangeScope.Subjects)
        {
            GroupType = default;
            CurrentGroup = null;
            RequestedGroup = null;

            foreach (var row in Subjects ?? [])
            {
                if (row is null)
                    continue;

                row.Subject = row.Subject?.Trim() ?? "";
                row.CurrentGroup = row.CurrentGroup?.Trim() ?? "";
                row.RequestedGroup = row.RequestedGroup?.Trim() ?? "";
            }
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Scope))
            yield return Error("Wybierz zakres zmiany grupy.", nameof(Scope));

        if (Scope == GroupChangeScope.General)
        {
            if (!Enum.IsDefined(GroupType))
                yield return Error("Wybierz rodzaj grupy.", nameof(GroupType));

            foreach (var error in ValidateGroups(CurrentGroup, RequestedGroup, ""))
                yield return error;
        }
        else if (Scope == GroupChangeScope.Subjects)
        {
            foreach (var error in ValidateSubjects())
                yield return error;
        }

        if (string.IsNullOrWhiteSpace(Justification))
            yield return Error("Podaj uzasadnienie.", nameof(Justification));
        else if (Justification.Length > MaxJustificationLength)
            yield return Error($"Uzasadnienie może mieć najwyżej {MaxJustificationLength} znaków.",
                nameof(Justification));
    }

    private IEnumerable<ValidationResult> ValidateSubjects()
    {
        var subjects = Subjects ?? [];

        if (subjects.Count == 0)
            yield return Error("Dodaj co najmniej jeden przedmiot.", nameof(Subjects));
        else if (subjects.Count > MaxSubjects)
            yield return Error($"Możesz podać najwyżej {MaxSubjects} przedmiotów.", nameof(Subjects));

        var seen = new HashSet<(string, GroupType)>();

        for (var i = 0; i < subjects.Count; i++)
        {
            var row = subjects[i];
            var prefix = $"{nameof(Subjects)}[{i}].";

            if (row is null)
            {
                yield return Error("Uzupełnij wiersz przedmiotu.", nameof(Subjects));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Subject))
                yield return Error("Podaj nazwę przedmiotu.", prefix + nameof(row.Subject));
            else if (row.Subject.Length > MaxSubjectLength)
                yield return Error($"Nazwa przedmiotu może mieć najwyżej {MaxSubjectLength} znaków.",
                    prefix + nameof(row.Subject));
            else if (!seen.Add((row.Subject.Trim().ToUpperInvariant(), row.GroupType)))
                yield return Error("Ten przedmiot z tym rodzajem zajęć już jest na liście.",
                    prefix + nameof(row.Subject));

            if (row.GroupType == GroupType.Dean)
                yield return Error("Grupę dziekańską zmienia się w wariancie ogólnym.",
                    prefix + nameof(row.GroupType));
            else if (!Enum.IsDefined(row.GroupType))
                yield return Error("Wybierz rodzaj zajęć.", prefix + nameof(row.GroupType));

            foreach (var error in ValidateGroups(row.CurrentGroup, row.RequestedGroup, prefix))
                yield return error;
        }
    }

    // prefix: "" for the general scope, "Subjects[i]." for a table row
    private static IEnumerable<ValidationResult> ValidateGroups(string? current, string? requested,
        string prefix)
    {
        var currentName = prefix + nameof(CurrentGroup);
        var requestedName = prefix + nameof(RequestedGroup);

        if (string.IsNullOrWhiteSpace(current))
            yield return Error("Podaj obecną grupę.", currentName);
        else if (current.Length > MaxGroupLength)
            yield return Error($"Numer grupy może mieć najwyżej {MaxGroupLength} znaków.", currentName);

        if (string.IsNullOrWhiteSpace(requested))
            yield return Error("Podaj wnioskowaną grupę.", requestedName);
        else if (requested.Length > MaxGroupLength)
            yield return Error($"Numer grupy może mieć najwyżej {MaxGroupLength} znaków.", requestedName);
        else if (string.Equals(current?.Trim(), requested.Trim(), StringComparison.OrdinalIgnoreCase))
            yield return Error("Wnioskowana grupa musi się różnić od obecnej.", requestedName);
    }

    private static ValidationResult Error(string message, string memberName) =>
        new(message, [memberName]);
}