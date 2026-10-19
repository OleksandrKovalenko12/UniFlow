using UniFlow.Domain.Entities;

namespace UniFlow.Tests.Domain;

public class FacultyTests
{
    [Fact]
    public void Constructor_ValidData_SetsCodeAndName()
    {
        var code = "WI";
        var name = "Wydział Informatyki";

        var faculty = new Faculty(code, name);

        Assert.Equal("WI", faculty.Code);
        Assert.Equal("Wydział Informatyki", faculty.Name);
    }

    [Fact]
    public void Constructor_ValuesWithSpaces_AreTrimmed()
    {
        var faculty = new Faculty("  WI  ", "  Wydział Informatyki  ");

        Assert.Equal("WI", faculty.Code);
        Assert.Equal("Wydział Informatyki", faculty.Name);
    }

    [Fact]
    public void Constructor_MixedCaseCode_KeepsOriginalCase()
    {
        var faculty = new Faculty("WIiSI", "Wydział Informatyki i Sztucznej Inteligencji");

        Assert.Equal("WIiSI", faculty.Code);
    }

    [Fact]
    public void Rename_ValidName_ChangesName()
    {
        var faculty = new Faculty("WI", "Wyzdiał Informatyki");

        faculty.Rename("Wydział Infromatyki i Sztucznej Inteligencji");

        Assert.Equal("Wydział Infromatyki i Sztucznej Inteligencji", faculty.Name);

    }

    [Fact]
    public void ChangeCode_ValidCode_ChangesCode()
    {
        var faculty = new Faculty("WI", "Wydział Informatyki");

        faculty.ChangeCode("WIiSI");

        Assert.Equal("WIiSI", faculty.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("W")]
    [InlineData("ADFGGJFHSAFFASF")]
    [InlineData("W-I")]
    [InlineData("W I")]
    public void Constructor_InvalidCode_Throws(string? code)
    {
        Assert.Throws<ArgumentException>(() => new Faculty(code!, "Wydział Informatyki"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("W")]
    [InlineData("ADFGGJFHSAFFASF")]
    [InlineData("W-I")]
    [InlineData("W I")]
    public void ChangeCode_InvalidCode_Throws(string? code)
    {
        var faculty = new Faculty("WI", "Wydzial Informatyki");

        Assert.Throws<ArgumentException>(() => faculty.ChangeCode(code!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_InvalidName_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Faculty("WI", name!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rename_InvalidName_Throws(string? name)
    {
        var faculty = new Faculty("WI", "Wydzial Informatyki");

        Assert.Throws<ArgumentException>(() => faculty.Rename(name!));
    }
}
