using UniFlow.Domain.Enums;
using UniFlow.Shared.Applications;

namespace UniFlow.Tests.Shared;

public class ApplicationStateTests
{
    // The Api turns one into the other with a cast - names and numbers must be the same
    [Fact]
    public void ApplicationState_MatchesDomainStatus()
    {
        var domain = Enum.GetValues<ApplicationStatus>().Select(s => ((int)s, s.ToString()));
        var shared = Enum.GetValues<ApplicationState>().Select(s => ((int)s, s.ToString()));

        Assert.Equal(domain, shared);
    }
}