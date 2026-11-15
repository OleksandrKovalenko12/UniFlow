using UniFlow.Client.Api;
using UniFlow.Shared.Drafts;

namespace UniFlow.Client.Applications;

// Saves a draft a moment after each change, one save at a time - for the new application form
// and for the correction page alike. Changes are counted, so a change made while a save is on
// its way is not taken as saved.
public sealed class DraftAutosave : IDisposable
{
    // How long after the last change the draft is saved
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1.5);

    private readonly Func<Task<ApiResult<DraftDetails>>> _save;
    private readonly Func<Task> _saved;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CancellationTokenSource? _delay;
    private int _changes;
    private bool _submitted;

    // save: sends the page's current form; saved: lets the page show the new state
    public DraftAutosave(Func<Task<ApiResult<DraftDetails>>> save, Func<Task> saved)
    {
        _save = save;
        _saved = saved;
    }

    public DraftDetails? Draft { get; private set; }
    public bool IsDirty { get; private set; }
    public bool IsSaving { get; private set; }
    public bool SaveFailed { get; private set; }

    // After a successful submission there is nothing left to lose
    public bool HasUnsavedChanges => !_submitted && (IsDirty || IsSaving);

    public string Status =>
        SaveFailed ? "Nie udało się zapisać szkicu - spróbujemy przy następnej zmianie."
        : IsSaving ? "Zapisywanie..."
        : Draft is not null ? $"Szkic zapisany {DateText.Format(Draft.UpdatedAt)}"
        : "";

    // A draft from before, continued
    public void Load(DraftDetails draft) => Draft = draft;

    // The draft was deleted on the server: the next change starts a new one
    public void Forget()
    {
        _delay?.Cancel();
        Draft = null;
        IsDirty = false;
        SaveFailed = false;
    }

    // Saved a moment after the last change, not after every key
    public void Changed()
    {
        IsDirty = true;
        _changes++;

        _delay?.Cancel();
        _delay = new CancellationTokenSource();
        _ = SaveLaterAsync(_delay.Token);
    }

    // At once, e.g. before leaving the page; false when the server did not take it
    public async Task<bool> SaveNowAsync()
    {
        _delay?.Cancel();
        await _lock.WaitAsync();
        try
        {
            // Submitted in the meantime: the server has deleted the draft, it must not come back
            if (_submitted)
                return true;
            if (!IsDirty && Draft is not null)
                return true;

            var changes = _changes;
            IsSaving = true;
            var result = await _save();
            IsSaving = false;

            if (!result.IsSuccess)
            {
                SaveFailed = true;
                return false;
            }

            Draft = result.Value;
            SaveFailed = false;

            // Changed while saving: still dirty, the scheduled save will take it
            if (_changes == changes)
                IsDirty = false;

            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    // A file needs a draft to go to - saved now if there is none yet
    public async Task<Guid?> EnsureDraftAsync()
    {
        if (Draft is null)
        {
            IsDirty = true;
            await SaveNowAsync();
        }

        return Draft?.Id;
    }

    // The submission runs with no save alongside: after it the server has deleted the draft,
    // and a late save would bring it back
    public async Task<ApiResult<T>> SubmitAsync<T>(Func<Task<ApiResult<T>>> submit)
    {
        _delay?.Cancel();
        await _lock.WaitAsync();
        try
        {
            var result = await submit();
            if (result.IsSuccess)
                _submitted = true;

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _delay?.Cancel();

    private async Task SaveLaterAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(Delay, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await SaveNowAsync();
        await _saved();
    }
}