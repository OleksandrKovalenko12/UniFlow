using UniFlow.Api.Texts;
using UniFlow.Documents.Models;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Time;

namespace UniFlow.Api.Applications;

// Turns system data into the texts printed on the document; the enrollment must be
// loaded with FieldOfStudy and Faculty
public static class ApplicationHeaderBuilder
{
    // Groups are usually known before 1 October; from 20 September on,
    // applications concern the coming academic year (assumption)
    private const int NewAcademicYearMonth = 9;
    private const int NewAcademicYearDay = 20;

    public static ApplicationHeader Build(string number, DateTime submittedAtUtc,
        string firstName, string lastName, string albumNumber, Enrollment enrollment)
    {
        var polishTime = PolishTime.FromUtc(submittedAtUtc);
        var field = enrollment.FieldOfStudy;

        return new ApplicationHeader(
            ApplicationNumber: number,
            SubmittedAt: polishTime,
            StudentFullName: $"{firstName} {lastName}",
            AlbumNumber: albumNumber,
            FacultyName: field.Faculty.Name,
            FieldOfStudy: field.Name,
            StudyForm: $"{StudyTexts.Mode(enrollment.StudyMode)}, {StudyTexts.Level(field.Level)}",
            Semester: enrollment.Semester,
            AcademicYear: AcademicYear(polishTime));
    }

    private static string AcademicYear(DateTime polishTime)
    {
        var newYearStart = new DateTime(polishTime.Year, NewAcademicYearMonth, NewAcademicYearDay);
        var startYear = polishTime >= newYearStart ? polishTime.Year : polishTime.Year - 1;

        return $"{startYear}/{startYear + 1}";
    }
}