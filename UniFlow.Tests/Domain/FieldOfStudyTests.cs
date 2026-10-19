using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;

namespace UniFlow.Tests.Domain;

public class FieldOfStudyTests
{
    private static Faculty CreateFaculty(string code = "WI") =>
        new(code, "Wydział Informatyki");

    [Fact]
    public void Constructor_ValidData_SetsAllProperties()
    {
        var faculty = CreateFaculty();

        var field = new FieldOfStudy("  Informatyka  ",
            StudyLevel.FirstCycle, faculty);

        Assert.Same(faculty, field.Faculty);
        Assert.Equal("Informatyka", field.Name);
        Assert.Equal(StudyLevel.FirstCycle, field.Level);
    }

    [Fact]
    public void Constructor_NullFaculty_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FieldOfStudy("Informatyka",
            StudyLevel.FirstCycle, null!));
    }

    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FieldOfStudy("",
            StudyLevel.FirstCycle, CreateFaculty()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Constructor_UndefinedLevel_Throws(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FieldOfStudy("Informatyka",
            (StudyLevel)level, CreateFaculty()));
    }

    [Fact]
    public void MoveToFaculty_NewFaculty_ChangesFaculty()
    {
        var field = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle,
            CreateFaculty("WI"));
        var newFaculty = CreateFaculty("WIiSI");

        field.MoveToFaculty(newFaculty);

        Assert.Same(newFaculty, field.Faculty);
    }

    [Fact]
    public void MoveToFaculty_Null_Throws()
    {
        var field = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle,
            CreateFaculty());

        Assert.Throws<ArgumentNullException>(() => field.MoveToFaculty(null!));
    }
}
