using MudBlazor;
using UniFlow.Shared.Applications;

namespace UniFlow.Client.Applications;

// The Polish name and the colour of every status - the same on every screen
public static class StatusLabels
{
    public static string Name(ApplicationState status) => status switch
    {
        ApplicationState.Submitted => "Złożony",
        ApplicationState.UnderReview => "W weryfikacji",
        ApplicationState.NeedsCorrection => "Do poprawy",
        ApplicationState.Resubmitted => "Złożony ponownie",
        ApplicationState.ForwardedToDean => "Przekazany prodziekanowi",
        ApplicationState.Approved => "Zatwierdzony",
        ApplicationState.Rejected => "Odrzucony",
        ApplicationState.Withdrawn => "Wycofany",
        _ => status.ToString()
    };

    // "Do poprawy" is orange, not red: the case is not lost, the student has something to do
    public static Color ColorOf(ApplicationState status) => status switch
    {
        ApplicationState.Submitted or ApplicationState.Resubmitted => Color.Info,
        ApplicationState.UnderReview => Color.Primary,
        ApplicationState.NeedsCorrection => Color.Warning,
        ApplicationState.ForwardedToDean => Color.Tertiary,
        ApplicationState.Approved => Color.Success,
        ApplicationState.Rejected => Color.Error,
        _ => Color.Default
    };
}