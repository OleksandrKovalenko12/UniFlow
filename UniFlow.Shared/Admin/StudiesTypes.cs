using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniFlow.Shared.Admin;

// The study mode as the browser sees it. Same names and numbers as StudyMode in Domain
// (the browser does not reference Domain) - a test keeps the two in step.
[JsonConverter(typeof(JsonStringEnumConverter<StudyModeOption>))]
public enum StudyModeOption
{
    FullTime = 1,
    PartTime = 2
}

// One of the student's studies, active or ended. MaxSemester: the last semester of this level -
// the semester list in the dialog goes up to it.
public sealed record StudiesItem(
    int Id,
    int FieldOfStudyId,
    string FieldOfStudyName,
    string StudyLevel,
    string FacultyName,
    int Semester,
    int MaxSemester,
    StudyModeOption StudyMode,
    bool IsActive);

// A field of study to choose when adding studies
public sealed record FieldOfStudyOption(
    int Id,
    string Name,
    string StudyLevel,
    string FacultyCode,
    int MaxSemester);

// New studies: which field, from which semester, in which mode. The semester's upper limit
// depends on the level - Domain checks it.
public sealed class AddStudiesRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Wybierz kierunek.")]
    public int FieldOfStudyId { get; set; }

    [Range(1, 12, ErrorMessage = "Wybierz semestr.")]
    public int Semester { get; set; }

    [EnumDataType(typeof(StudyModeOption), ErrorMessage = "Wybierz tryb studiów.")]
    public StudyModeOption StudyMode { get; set; }
}

// Active studies: the semester and the mode
public sealed class ChangeStudiesRequest
{
    [Range(1, 12, ErrorMessage = "Wybierz semestr.")]
    public int Semester { get; set; }

    [EnumDataType(typeof(StudyModeOption), ErrorMessage = "Wybierz tryb studiów.")]
    public StudyModeOption StudyMode { get; set; }
}

// Ended studies taken up again - from which semester
public sealed class ResumeStudiesRequest
{
    [Range(1, 12, ErrorMessage = "Wybierz semestr.")]
    public int Semester { get; set; }
}