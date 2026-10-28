using UniFlow.Domain.Entities;

namespace UniFlow.Tests.Domain;

public class ApplicationTypeTests
{
    private static ApplicationType CreateType(string code = "GROUP_CHANGE", int maxAttachments = 5)
        => new(code, "Zmiana grupy", "Złóż, jeśli chcesz zmienić grupę zajęciową.", true, maxAttachments);

    [Fact]
    public void Constructor_ValidData_SetsPropertiesAndIsActive()
    {
        var type = new ApplicationType(
            "PERSONAL_DATA_CHANGE",
            "  Zmiana danych osobowych  ",
            "Wymagany dokument potwierdzający zmianę,",
            requiresDeanDecision: false,
            maxAttachments: 3);

        Assert.Equal(3, type.MaxAttachments);
        Assert.Equal("PERSONAL_DATA_CHANGE", type.Code);
        Assert.Equal("Zmiana danych osobowych", type.Name);
        Assert.False(type.RequiresDeanDecision);
        Assert.True(type.IsActive);
    }

    [Theory]
    [InlineData("PERSONAL_DATA_CHANGE")]
    [InlineData("GROUP_CHANGE")]
    [InlineData("A")]
    [InlineData("TYPE2")]
    public void Constructor_ValidCode_IsAccepted(string code)
    {
        var type = CreateType(code);

        Assert.Equal(code, type.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("personal_data")]
    [InlineData("_GROUP")]
    [InlineData("2TYPE")]
    [InlineData(" GROUP_CHANGE")]
    [InlineData("GROUP-CHANGE")]
    public void Constructor_InvalidCode_Throws(string? code)
    {
        var ex = Assert.Throws<ArgumentException>(() => CreateType(code!));
        Assert.Equal("code", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void Constructor_MaxAttachmentsAtBoundary_IsAccepted(int maxAttachments)
    {
        var type = CreateType(maxAttachments: maxAttachments);

        Assert.Equal(maxAttachments, type.MaxAttachments);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void Constructor_MaxAttachmentsOutOfRange_Throws(int maxAttachments)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateType(maxAttachments: maxAttachments));

        Assert.Equal("maxAttachments", ex.ParamName);
    }

    [Fact]
    public void Constructor_EmptyDescription_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ApplicationType("GROUP_CHANGE",
            "Zmiana grupy", "", true, 5));

        Assert.Equal("description", ex.ParamName);
    }

    [Fact]
    public void DeactivateAndActivate_ChangeIsActive()
    {
        var type = CreateType();

        type.Deactivate();
        Assert.False(type.IsActive);

        type.Activate();
        Assert.True(type.IsActive);
    }

    [Fact]
    public void SetRequiresDeanDecision_ChangesFlag()
    {
        var type = CreateType();

        type.SetRequiresDeanDecision(false);

        Assert.False(type.RequiresDeanDecision);
    }

    [Fact]
    public void SetMaxAttachments_ValidValue_ChangesLimit()
    {
        var type = CreateType(maxAttachments: 5);

        type.SetMaxAttachments(2);

        Assert.Equal(2, type.MaxAttachments);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void SetMaxAttachments_OutOfRange_ThrowsAndKeepsOldValue(int maxAttachments)
    {
        var type = CreateType(maxAttachments: 5);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            type.SetMaxAttachments(maxAttachments));

        Assert.Equal("maxAttachments", ex.ParamName);
        Assert.Equal(5, type.MaxAttachments);
    }
}
