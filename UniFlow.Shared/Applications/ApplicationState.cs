using System.Text.Json.Serialization;

namespace UniFlow.Shared.Applications;

// The status as the browser sees it. Same names and numbers as ApplicationStatus in Domain
// (the browser does not reference Domain) - a test keeps the two in step.
[JsonConverter(typeof(JsonStringEnumConverter<ApplicationState>))]
public enum ApplicationState
{
    Submitted = 1,
    UnderReview = 2,
    NeedsCorrection = 3,
    Resubmitted = 4,
    ForwardedToDean = 5,
    Approved = 6,
    Rejected = 7,
    Withdrawn = 8
}