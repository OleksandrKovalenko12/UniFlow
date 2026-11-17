using UniFlow.Api.Auth;
using UniFlow.Shared.Auth;

namespace UniFlow.Tests.Api;

public class TemporaryPasswordsTests
{
    // Random - so many times; a password breaking the rules would make creating an account fail
    [Fact]
    public void Generate_AlwaysMeetsThePasswordRules()
    {
        for (var i = 0; i < 1000; i++)
        {
            var password = TemporaryPasswords.Generate();

            Assert.True(password.Length >= PasswordRules.MinLength);
            Assert.Contains(password, char.IsAsciiLetterUpper);
            Assert.Contains(password, char.IsAsciiLetterLower);
            Assert.Contains(password, char.IsAsciiDigit);
            Assert.Contains(password, c => !char.IsLetterOrDigit(c));
        }
    }
}