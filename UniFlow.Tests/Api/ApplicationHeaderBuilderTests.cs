using System.Globalization;
using UniFlow.Api.Applications;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;

namespace UniFlow.Tests.Api;

public class ApplicationHeaderBuilderTests
{
    private static Enrollment CreateEnrollment()
    {
        var faculty = new Faculty("WT", "Wydział Testowy");
        var field = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle, faculty);

        return new Enrollment("student-1", field, 3, StudyMode.FullTime);
    }

    [Theory]
    [InlineData("2026-09-19T21:30:00Z", "2025/2026")] // 19.09, 23:30 in Poland
    [InlineData("2026-09-19T22:00:00Z", "2026/2027")] // 20.09, 00:00 in Poland
    [InlineData("2027-01-15T12:00:00Z", "2026/2027")]
    public void Build_UsesPolishTimeForAcademicYear(string utc, string expected)
    {
        var submittedAt = DateTime.Parse(utc, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal);

        var header = ApplicationHeaderBuilder.Build("UF/2026/0001", submittedAt, "Anna", "Testowa",
            "100001", CreateEnrollment());

        Assert.Equal(expected, header.AcademicYear);
    }
}