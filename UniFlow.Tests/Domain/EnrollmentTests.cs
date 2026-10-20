using System;
using System.Collections.Generic;
using System.Text;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;

namespace UniFlow.Tests.Domain;

public class EnrollmentTests
{
    private static FieldOfStudy CreateField(StudyLevel level = StudyLevel.FirstCycle) =>
        new("Informatyka", level, new Faculty("WI", "Wydział Informatyki"));

    private static Enrollment CreateEnrollment(int semester = 1) =>
        new("student-1", CreateField(), semester, StudyMode.FullTime);

    //Constructor tests
    [Fact]
    public void Constructor_ValidData_SetsProperties()
    {
        var field = CreateField();

        var enrollment = new Enrollment("student-1", field, 3, StudyMode.PartTime);

        Assert.Equal("student-1", enrollment.StudentId);
        Assert.Same(field, enrollment.FieldOfStudy);
        Assert.Equal(3, enrollment.Semester);
        Assert.Equal(StudyMode.PartTime, enrollment.StudyMode);
        Assert.True(enrollment.IsActive);
    }

    [Theory]
    [InlineData(StudyLevel.FirstCycle, 8)]
    [InlineData(StudyLevel.SecondCycle, 4)]
    [InlineData(StudyLevel.LongCycle, 12)]
    public void Constructor_MaxSemesterForLevel_IsAccepted(StudyLevel level, int maxSemester)
    {
        var enrollment = new Enrollment("student-1", CreateField(level), maxSemester, StudyMode.FullTime);

        Assert.Equal(maxSemester, enrollment.Semester);
    }

    [Theory]
    [InlineData(StudyLevel.FirstCycle, 9)]
    [InlineData(StudyLevel.SecondCycle, 5)]
    [InlineData(StudyLevel.LongCycle, 13)]
    [InlineData(StudyLevel.FirstCycle, 0)]
    [InlineData(StudyLevel.FirstCycle, -1)]
    public void Constructor_SemesterOutOfRange_Throws(StudyLevel level, int semester)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Enrollment("student-1", CreateField(level), semester, StudyMode.FullTime));
    }

    //YearOfStudy test
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(7, 4)]
    public void YearOfStudy_IsCalculatedFromSemester(int semester, int expectedYear)
    {
        var enrollment = CreateEnrollment(semester);

        Assert.Equal(expectedYear, enrollment.YearOfStudy);
    }

    //Active and inactive tests
    [Fact]
    public void ChangeSemester_Inactive_Throws()
    {
        var enrollment = CreateEnrollment();
        enrollment.Deactivate();

        Assert.Throws<InvalidOperationException>(() => enrollment.ChangeSemester(2));
    }

    [Fact]
    public void ChangeStudyMode_Inactive_Throws()
    {
        var enrollment = CreateEnrollment();
        enrollment.Deactivate();

        Assert.Throws<InvalidOperationException>(() => enrollment.ChangeStudyMode(StudyMode.PartTime));
    }

    [Fact]
    public void Deactivate_AlreadyInactive_Throws()
    {
        var enrollment = CreateEnrollment();
        enrollment.Deactivate();

        Assert.Throws<InvalidOperationException>(() => enrollment.Deactivate());
    }

    [Fact]
    public void Reactivate_Inactive_SetsSemesterAndActivates()
    {
       var enrollment = CreateEnrollment(semester: 5);
        enrollment.Deactivate();

        enrollment.Reactivate(3);

        Assert.True(enrollment.IsActive);
        Assert.Equal(3, enrollment.Semester);
    }

    [Fact]
    public void Reactivate_AlreadyActive_Throws()
    {
        var enrollment = CreateEnrollment();

        Assert.Throws<InvalidOperationException>(() => enrollment.Reactivate(1));
    }
}
