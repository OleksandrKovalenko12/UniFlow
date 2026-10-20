using UniFlow.Domain.Entities;

namespace UniFlow.Tests.Domain;

public class StudentProfileTests
{
    [Fact]
    public void Constructor_ValidData_SetsProperties()
    {
        var profile = new StudentProfile("user-1", "012345", "jan@example.com");

        Assert.Equal("user-1", profile.UserId);
        Assert.Equal("012345", profile.AlbumNumber);
        Assert.Equal("jan@example.com", profile.PrivateEmail);
    }

    [Fact]
    public void Constructor_WithoutEmail_EmailIsNull()
    {
        var profile = new StudentProfile("user-1", "012345");

        Assert.Null(profile.PrivateEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345678901")]
    [InlineData(" 123456")]
    [InlineData("123a456")]
    [InlineData("١٢٣٤٥٦")]
    public void Constructor_InvalidAlbumNumber_Throws(string albumNumber)
    {
        var ex = Assert.Throws<ArgumentException>(() => new StudentProfile("user-1",
            albumNumber));

        Assert.Equal("albumNumber", ex.ParamName);
    }

    [Theory]
    [InlineData("not-an-mail")]
    [InlineData("jan@")]
    [InlineData("Jan Kowalski <jan@example.com>")]
    public void Constructor_InvalidEmail_Throws(string email)
    {
        var ex = Assert.Throws<ArgumentException>(() => new StudentProfile("user-1",
            "123456", email));

        Assert.Equal("email", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ChangePrivateEmail_Empty_RemovesEmail(string? newEmail)
    {
        var profile = new StudentProfile("user-1", "123456", "jan@example.com");

        profile.ChangePrivateEmail(newEmail);

        Assert.Null(profile.PrivateEmail);
    }
}
