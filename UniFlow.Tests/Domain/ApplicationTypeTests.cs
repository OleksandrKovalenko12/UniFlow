using UniFlow.Domain.Entities;

namespace UniFlow.Tests.Domain;

public class ApplicationTypeTests
{
    private static ApplicationType CreateType(string code = "GROUP_CHANGE") 
        => new(code, "Zmiana grupy", "Złóż, jeśli chcecz zmienić grupę zajęciową.", true);

    [Fact]
    public void Constructor_ValidData_SetsPropertiesAndIsActive()
    {
        var type = new ApplicationType(
            "PERSONAL_DATA_CHANGE",
            "  Zmiana danych osobowych  ",
            "Wymagany dokument potwierdzający zmianę,",
            requiresDeanDecision: false);

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

    [Fact]
    public void Constructor_EmptyDescription_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ApplicationType("GROUP_CHANGE",
            "Zmiana grupy", "", true));
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
}
