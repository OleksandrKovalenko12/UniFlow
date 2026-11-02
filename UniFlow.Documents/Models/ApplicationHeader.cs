namespace UniFlow.Documents.Models;

public sealed record ApplicationHeader(
    string ApplicationNumber,
    DateTime SubmittedAt,
    string StudentFullName,
    string AlbumNumber,
    string FacultyName,
    string FieldOfStudy,
    string StudyForm,
    int Semester,
    string AcademicYear);