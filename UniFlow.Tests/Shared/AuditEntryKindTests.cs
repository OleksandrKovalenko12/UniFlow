using UniFlow.Domain.Enums;
using UniFlow.Shared.Admin;

namespace UniFlow.Tests.Shared;

public class AuditEntryKindTests
{
    // The Api turns one into the other with a cast - names and numbers must be the same
    [Fact]
    public void AuditEntryKind_MatchesDomainAuditAction()
    {
        var domain = Enum.GetValues<AuditAction>().Select(a => ((int)a, a.ToString()));
        var shared = Enum.GetValues<AuditEntryKind>().Select(a => ((int)a, a.ToString()));

        Assert.Equal(domain, shared);
    }

    // Every kind can be found with the "Rodzaj czynności" filter
    [Fact]
    public void AuditGroups_TogetherCoverEveryKind()
    {
        var grouped = Enum.GetValues<AuditGroup>().SelectMany(AuditGroups.KindsOf).Order();

        Assert.Equal(Enum.GetValues<AuditEntryKind>().Order(), grouped);
    }
}