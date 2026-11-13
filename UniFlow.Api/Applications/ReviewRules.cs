using UniFlow.Domain.Enums;
using UniFlow.Shared.Review;

namespace UniFlow.Api.Applications;

// The transition table from docs/model-domeny.md, seen from the employee's screen.
// It only decides which buttons to show - Domain still checks every rule when one is clicked.
public static class ReviewRules
{
    public static IReadOnlyList<ReviewAction> ActionsFor(ApplicationStatus status,
        bool requiresDeanDecision, bool isDeanOffice, bool isViceDean)
    {
        var actions = new List<ReviewAction>();

        if (isDeanOffice)
        {
            switch (status)
            {
                case ApplicationStatus.Submitted or ApplicationStatus.Resubmitted:
                    actions.Add(ReviewAction.Take);
                    break;

                case ApplicationStatus.UnderReview:
                    // Either the dean's office decides, or it passes the case to the vice-dean
                    actions.Add(requiresDeanDecision ? ReviewAction.ForwardToDean : ReviewAction.Approve);
                    actions.Add(ReviewAction.RequestCorrection);
                    actions.Add(ReviewAction.Reject);
                    break;

                case ApplicationStatus.NeedsCorrection:
                    // The student did not answer
                    actions.Add(ReviewAction.Reject);
                    break;
            }
        }

        if (isViceDean && status == ApplicationStatus.ForwardedToDean)
            actions.AddRange([ReviewAction.Approve, ReviewAction.Reject, ReviewAction.ReturnToDeanOffice]);

        return actions;
    }

    // The student may change their mind until the case is with the vice-dean
    public static bool CanWithdraw(ApplicationStatus status) =>
        status is ApplicationStatus.Submitted or ApplicationStatus.UnderReview
            or ApplicationStatus.NeedsCorrection or ApplicationStatus.Resubmitted;

    public static ApplicationStatus[] StatusesOf(ReviewTab tab) => tab switch
    {
        ReviewTab.New => [ApplicationStatus.Submitted, ApplicationStatus.Resubmitted],
        ReviewTab.InReview => [ApplicationStatus.UnderReview],
        ReviewTab.WaitingForStudent => [ApplicationStatus.NeedsCorrection],
        ReviewTab.AtViceDean => [ApplicationStatus.ForwardedToDean],
        ReviewTab.Closed => [ApplicationStatus.Approved, ApplicationStatus.Rejected,
            ApplicationStatus.Withdrawn],
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "Unknown tab.")
    };
}