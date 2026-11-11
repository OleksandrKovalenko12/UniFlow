using UniFlow.Shared.Enrollments;

namespace UniFlow.Client.Applications;

// How studies are described on screen - the same in the dialog and on the form
public static class EnrollmentLabels
{
    // "studia pierwszego stopnia, stacjonarne"
    public static string Details(EnrollmentSummary enrollment) =>
        $"{enrollment.StudyLevel}, {enrollment.StudyMode}";

    // "Wydział Informatyki i Telekomunikacji · semestr 3"
    public static string Faculty(EnrollmentSummary enrollment) =>
        $"{enrollment.FacultyName} · semestr {enrollment.Semester}";
}