using UniFlow.Domain.Enums;
using UniFlow.Shared.Admin;

namespace UniFlow.Tests.Shared;

public class StudyLevelOptionTests
{
    // The Api turns one into the other with a cast - names and numbers must be the same
    [Fact]
    public void StudyLevelOption_MatchesDomainStudyLevel()
    {
        var domain = Enum.GetValues<StudyLevel>().Select(l => ((int)l, l.ToString()));
        var shared = Enum.GetValues<StudyLevelOption>().Select(l => ((int)l, l.ToString()));

        Assert.Equal(domain, shared);
    }
}