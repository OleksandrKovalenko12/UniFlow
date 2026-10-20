using UniFlow.Domain.Entities;

namespace UniFlow.Tests.Domain;

public class StaffAssignmentTests
{
    private static Faculty CreateFaculty() => new("WI", "Wydział Informatyki");

    [Fact]
    public void Constructor_ValidData_SetsProperties()
    {
        var faculty = CreateFaculty();

        var assignment = new StaffAssignment("user-1", faculty);

        Assert.Equal("user-1", assignment.UserId);
        Assert.Same(faculty, assignment.Faculty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Constructor_EmptyUserId_Throws(string? userId)
    {
        var ex = Assert.Throws<ArgumentException>(() => new StaffAssignment(userId!,
            CreateFaculty()));

        Assert.Equal("userId", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullFaculty_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new StaffAssignment("user-1", null!));
    }
}
