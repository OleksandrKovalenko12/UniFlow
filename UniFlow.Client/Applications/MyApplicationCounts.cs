using UniFlow.Client.Api;

namespace UniFlow.Client.Applications;

// The numbers next to "W toku" and "Zakończone" in the student's menu, kept in one place: the
// menu shows them, the pages that change them say so. "W toku" counts unfinished drafts too -
// the same as the list.
public sealed class MyApplicationCounts
{
    private readonly ApiClient _api;

    public MyApplicationCounts(ApiClient api) => _api = api;

    // null = not loaded yet, the menu shows no number
    public int? InProgress { get; private set; }
    public int? Closed { get; private set; }

    public event Action? Changed;

    // From the lists a page has just loaded - no second request
    public void Set(int inProgress, int closed)
    {
        InProgress = inProgress;
        Closed = closed;
        Changed?.Invoke();
    }

    // At the start and after something moves an application (submitted, withdrawn)
    public async Task RefreshAsync()
    {
        var items = _api.GetMyApplicationsAsync();
        var drafts = _api.GetMyDraftsAsync();

        var itemsResult = await items;
        var draftsResult = await drafts;

        // The menu keeps the old numbers - not worth an error message
        if (!itemsResult.IsSuccess || !draftsResult.IsSuccess)
            return;

        Set(itemsResult.Value.Count(a => a.InProgress) + draftsResult.Value.Count,
            itemsResult.Value.Count(a => !a.InProgress));
    }
}