using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;

namespace UniFlow.Tests.Domain;

public class ApplicationTests
{
    private const string StudentId = "student-1";
    private static readonly DateTime Now = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Enrollment CreateEnrollment(string studentId = StudentId)
    {
        var faculty = new Faculty("WI", "Wydział Informatyki");
        var field = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle, faculty);
        return new Enrollment(studentId, field, 3, StudyMode.FullTime);
    }

    private static ApplicationType CreateType(bool requiresDeanDecision = true) =>
        new("GROUP_CHANGE", "Zmiana grupy", "Opis typu.", requiresDeanDecision);

    private static Application Submit(
        Enrollment? enrollment = null,
        ApplicationType? type = null,
        string applicantId = StudentId) =>
        new("UF/2026/0001", applicantId, enrollment ?? CreateEnrollment(), type ?? CreateType(),
            "{}", Now);

    //Right submissions

    [Fact]
    public void Constructor_ValidData_CreatesSubmittedApplication()
    {
        var application = Submit();

        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal("UF/2026/0001", application.Number);
        Assert.Equal(StudentId, application.ApplicantId);
        Assert.Equal(ApplicationStatus.Submitted, application.Status);
        Assert.Null(application.AssignedToId);
        Assert.Equal(Now, application.CreatedAt);
    }

    [Fact]
    public void Constructor_CreatesFirstHistoryEntry()
    {
        var application = Submit();

        var entry = Assert.Single(application.StatusHistory);
        Assert.Null(entry.FromStatus);
        Assert.Equal(ApplicationStatus.Submitted, entry.ToStatus);
        Assert.Equal(StudentId, entry.ChangedById);
        Assert.Equal(Now, entry.ChangedAt);
        Assert.False(entry.IsInternal);
    }

    [Fact]
    public void Constructor_CopiesRequiresDeanDecision_ChangeOfTypeDoesNotAffectApplication()
    {
        var type = CreateType(requiresDeanDecision: false);
        var application = Submit(type: type);

        type.SetRequiresDeanDecision(true);   // statute is changed

        Assert.False(application.RequiresDeanDecision);
    }

    //Bizness rules

    [Fact]
    public void Constructor_OtherStudentsEnrollment_Throws()
    {
        var enrollmentOfOtherStudent = CreateEnrollment("student-2");

        Assert.Throws<InvalidOperationException>(
            () => Submit(enrollment: enrollmentOfOtherStudent, applicantId: StudentId));
    }

    [Fact]
    public void Constructor_InactiveEnrollment_Throws()
    {
        var enrollment = CreateEnrollment();
        enrollment.Deactivate();

        Assert.Throws<InvalidOperationException>(() => Submit(enrollment: enrollment));
    }

    [Fact]
    public void Constructor_InactiveType_Throws()
    {
        var type = CreateType();
        type.Deactivate();

        Assert.Throws<InvalidOperationException>(() => Submit(type: type));
    }

    //Walidation of data

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyFormData_Throws(string? formData)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new Application("UF/2026/0001", StudentId, CreateEnrollment(), CreateType(),
                formData!, Now));
    }

    [Fact]
    public void Constructor_LocalTime_Throws()
    {
        var localTime = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(
            () => new Application("UF/2026/0001", StudentId, CreateEnrollment(), CreateType(),
                "{}", localTime));
    }
}