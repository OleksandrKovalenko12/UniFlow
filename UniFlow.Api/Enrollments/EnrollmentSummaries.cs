using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Texts;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Shared.Enrollments;

namespace UniFlow.Api.Enrollments;

// A student's active studies as the browser shows them - the same on the form and in the profile
public static class EnrollmentSummaries
{
    public static async Task<IReadOnlyList<EnrollmentSummary>> ActiveOfAsync(UniFlowDbContext db,
        string studentId, CancellationToken cancellationToken)
    {
        var enrollments = await db.Enrollments
            .Where(e => e.StudentId == studentId && e.IsActive)
            .OrderBy(e => e.FieldOfStudy.Name)
            .ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                FacultyName = e.FieldOfStudy.Faculty.Name,
                FieldOfStudyName = e.FieldOfStudy.Name,
                e.FieldOfStudy.Level,
                e.StudyMode,
                e.Semester,
            })
            .ToListAsync(cancellationToken);

        return enrollments
            .Select(e => new EnrollmentSummary(e.Id, e.FacultyName, e.FieldOfStudyName,
                StudyTexts.Level(e.Level), StudyTexts.Mode(e.StudyMode), e.Semester))
            .ToList();
    }
}