using UniFlow.Domain.Enums;
using UniFlow.Shared.Admin;

namespace UniFlow.Tests.Shared;

public class StudyModeOptionTests
{
    // The Api turns one into the other with a cast - names and numbers must be the same
    [Fact]
    public void StudyModeOption_MatchesDomainStudyMode()
    {
        var domain = Enum.GetValues<StudyMode>().Select(m => ((int)m, m.ToString()));
        var shared = Enum.GetValues<StudyModeOption>().Select(m => ((int)m, m.ToString()));

        Assert.Equal(domain, shared);
    }
}