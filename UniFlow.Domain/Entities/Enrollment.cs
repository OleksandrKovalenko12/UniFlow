using UniFlow.Domain.Enums;

namespace UniFlow.Domain.Entities;

public class Enrollment
{
    public int Id { get; private set; }
    public string StudentId { get; private set; } = null!;
    public int FieldOfStudyId { get; private set; }
    public FieldOfStudy FieldOfStudy { get; private set; } = null!;
    public int Semester { get; private set; }
    public StudyMode StudyMode { get; private set; }
    public bool IsActive { get; private set; }

    public int YearOfStudy => (Semester + 1) / 2;

    public Enrollment(string studentId, FieldOfStudy fieldOfStudy, int semester, StudyMode studyMode)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            throw new ArgumentException("StudentId is required.", nameof(studentId));

        ArgumentNullException.ThrowIfNull(fieldOfStudy);

        if (!Enum.IsDefined(studyMode))
            throw new ArgumentOutOfRangeException(nameof(studyMode), studyMode, "Unknown study mode.");

        StudentId = studentId;
        FieldOfStudy = fieldOfStudy;
        FieldOfStudyId = fieldOfStudy.Id;
        Semester = ValidateSemester(semester, fieldOfStudy.Level);
        IsActive = true;
        StudyMode = studyMode;
    }

    private Enrollment() { }

    public void ChangeSemester(int newSemester)
    {
        EnsureActive();
        Semester = ValidateSemester(newSemester, FieldOfStudy.Level);
    }

    public void ChangeStudyMode(StudyMode newMode)
    {
        EnsureActive();

        if (!Enum.IsDefined(newMode))
            throw new ArgumentOutOfRangeException(nameof(newMode), newMode, "Unknown study mode.");

        StudyMode = newMode;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Enrollment is already inactive");

        IsActive = false;
    }
    public void Reactivate(int semester)
    {
        if (IsActive)
            throw new InvalidOperationException("Enrollment is already active.");

        Semester = ValidateSemester(semester, FieldOfStudy.Level);
        IsActive = true;
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot modify an inactive enrollment.");
    }

    private static int GetMaxSemester(StudyLevel level) => level switch
    {
        StudyLevel.FirstCycle => 8,
        StudyLevel.SecondCycle => 4,
        StudyLevel.LongCycle => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown study level.")
    };

    private static int ValidateSemester(int semester, StudyLevel level)
    {
        var max = GetMaxSemester(level);

        if (semester < 1 || semester > max)
            throw new ArgumentOutOfRangeException(nameof(semester), semester,
                $"Semester must be between 1 and {max} for {level}.");

        return semester;
    }
}
