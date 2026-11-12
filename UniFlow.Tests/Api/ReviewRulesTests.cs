using UniFlow.Api.Applications;
using UniFlow.Domain.Enums;
using UniFlow.Shared.Review;

namespace UniFlow.Tests.Api;

public class ReviewRulesTests
{
    private static ReviewAction[] DeanOffice(ApplicationStatus status, bool requiresDeanDecision = true) =>
        [.. ReviewRules.ActionsFor(status, requiresDeanDecision, isDeanOffice: true, isViceDean: false)];

    private static ReviewAction[] ViceDean(ApplicationStatus status) =>
        [.. ReviewRules.ActionsFor(status, requiresDeanDecision: true, isDeanOffice: false, isViceDean: true)];

    [Theory]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Resubmitted)]
    public void DeanOffice_NewApplication_CanOnlyTakeIt(ApplicationStatus status)
    {
        Assert.Equal([ReviewAction.Take], DeanOffice(status));
    }

    [Fact]
    public void DeanOffice_UnderReview_ForwardsWhenTheViceDeanDecides()
    {
        Assert.Equal(
            [ReviewAction.ForwardToDean, ReviewAction.RequestCorrection, ReviewAction.Reject],
            DeanOffice(ApplicationStatus.UnderReview, requiresDeanDecision: true));
    }

    [Fact]
    public void DeanOffice_UnderReview_ApprovesWhenItDecidesItself()
    {
        Assert.Equal(
            [ReviewAction.Approve, ReviewAction.RequestCorrection, ReviewAction.Reject],
            DeanOffice(ApplicationStatus.UnderReview, requiresDeanDecision: false));
    }

    [Fact]
    public void DeanOffice_NeedsCorrection_CanOnlyReject()
    {
        Assert.Equal([ReviewAction.Reject], DeanOffice(ApplicationStatus.NeedsCorrection));
    }

    [Fact]
    public void ViceDean_Forwarded_DecidesOrReturns()
    {
        Assert.Equal(
            [ReviewAction.Approve, ReviewAction.Reject, ReviewAction.ReturnToDeanOffice],
            ViceDean(ApplicationStatus.ForwardedToDean));
    }

    // Nobody acts on a case that is with someone else or already closed
    [Theory]
    [InlineData(ApplicationStatus.ForwardedToDean, true, false)]
    [InlineData(ApplicationStatus.UnderReview, false, true)]
    [InlineData(ApplicationStatus.Approved, true, true)]
    [InlineData(ApplicationStatus.Rejected, true, true)]
    [InlineData(ApplicationStatus.Withdrawn, true, true)]
    public void NoActions_WhenTheCaseIsNotYours(ApplicationStatus status, bool isDeanOffice,
        bool isViceDean)
    {
        Assert.Empty(ReviewRules.ActionsFor(status, true, isDeanOffice, isViceDean));
    }

    // Every tab has its statuses and every status is on exactly one tab
    [Fact]
    public void StatusesOf_CoverEveryStatusOnce()
    {
        var onTabs = Enum.GetValues<ReviewTab>().SelectMany(ReviewRules.StatusesOf).ToList();

        Assert.Equal(Enum.GetValues<ApplicationStatus>().Order(), onTabs.Order());
    }
}