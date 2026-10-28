using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;

namespace UniFlow.Tests.Domain;

public class AuditLogEntryTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_ValidData_SetsProperties()
    {
        var entry = new AuditLogEntry("admin-1", AuditAction.RoleAssigned, "user-1",
            "  Role: DeanOffice  ", Now);

        Assert.Equal("admin-1", entry.ActorId);
        Assert.Equal(AuditAction.RoleAssigned, entry.Action);
        Assert.Equal("user-1", entry.TargetUserId);
        Assert.Equal("Role: DeanOffice", entry.Details);
        Assert.Equal(Now, entry.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyActorId_Throws(string? actorId)
    {
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            new AuditLogEntry(actorId!, AuditAction.RoleAssigned, null, null, Now));

        Assert.Equal("actorId", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Constructor_UndefinedAction_Throws(int action)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuditLogEntry("admin-1", (AuditAction)action, null, null, Now));

        Assert.Equal("action", ex.ParamName);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Constructor_NonUtcTime_Throws(DateTimeKind kind)
    {
        var time = DateTime.SpecifyKind(Now, kind);

        var ex = Assert.Throws<ArgumentException>(() =>
            new AuditLogEntry("admin-1", AuditAction.RoleAssigned, null, null, time));

        Assert.Equal("createdAt", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOptionalValues_AreNull(string? value)
    {
        var entry = new AuditLogEntry("admin-1", AuditAction.FacultyCreated, value, value, Now);

        Assert.Null(entry.TargetUserId);
        Assert.Null(entry.Details);
    }

    [Fact]
    public void Constructor_DetailsAtMaxLength_IsAccepted()
    {
        var details = new string('x', 2000);

        var entry = new AuditLogEntry("admin-1", AuditAction.FacultyChanged, null, details, Now);

        Assert.Equal(details, entry.Details);
    }

    [Fact]
    public void Constructor_DetailsTooLong_Throws()
    {
        var details = new string('x', 2001);

        var ex = Assert.Throws<ArgumentException>(() =>
            new AuditLogEntry("admin-1", AuditAction.FacultyChanged, null, details, Now));

        Assert.Equal("details", ex.ParamName);
    }

    [Fact]
    public void Constructor_TargetUserIdTooLong_Throws()
    {
        var targetUserId = new string('x', 451);

        var ex = Assert.Throws<ArgumentException>(() =>
            new AuditLogEntry("admin-1", AuditAction.RoleAssigned, targetUserId, null, Now));

        Assert.Equal("targetUserId", ex.ParamName);
    }
}