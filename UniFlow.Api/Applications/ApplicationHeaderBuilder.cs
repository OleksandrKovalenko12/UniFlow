using UniFlow.Api.Texts;
using UniFlow.Documents.Models;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Time;
using UniFlow.Shared;

namespace UniFlow.Api.Applications;

// Turns system data into the texts printed on the document; the enrollment must be
// loaded with FieldOfStudy and Faculty
public static class ApplicationHeaderBuilder
{
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
            AcademicYear: AcademicYear.Of(polishTime));
    }
}