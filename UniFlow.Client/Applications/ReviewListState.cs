using UniFlow.Shared.Review;

namespace UniFlow.Client.Applications;

// The list's tab and filters survive opening an application and coming back
public sealed class ReviewListState
{
    public ReviewTab? Tab { get; set; }
    public string? Search { get; set; }
    public string Type { get; set; } = "";
}