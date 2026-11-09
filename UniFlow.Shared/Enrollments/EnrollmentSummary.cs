namespace UniFlow.Shared.Enrollments;

// One study programme of the logged-in student, shown in the application wizard
public sealed record EnrollmentSummary(
    int Id,
    string FacultyName,
    string FieldOfStudyName,
    string StudyLevel,
    string StudyMode,
    int Semester);