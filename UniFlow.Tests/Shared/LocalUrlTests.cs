using UniFlow.Shared.Auth;

namespace UniFlow.Tests.Shared;

public class LocalUrlTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/new", true)]
    [InlineData("/applications/5?tab=history", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("new", false)]
    [InlineData("https://obcy.pl", false)]
    [InlineData("//another.pl", false)]
    [InlineData("/\\another.pl", false)]
    [InlineData("/\t/another.pl", false)]
    [InlineData("javascript:alert(1)", false)]
    public void IsLocal_AcceptsOnlyPathsInsideTheApplication(string? url, bool expected)
    {
        Assert.Equal(expected, LocalUrl.IsLocal(url));
    }
}