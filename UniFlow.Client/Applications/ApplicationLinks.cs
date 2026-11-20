using UniFlow.Shared.ApplicationTypes;
using UniFlow.Shared.Drafts;

namespace UniFlow.Client.Applications;

// Where each kind of application has its form - one place for every page that links to them
public static class ApplicationLinks
{
    // The correction page of an application sent back; null for a kind without one
    public static string? Correction(string typeCode, Guid applicationId) => typeCode switch
    {
        ApplicationTypeCodes.GroupChange => $"applications/{applicationId}/correct/group-change",
        _ => null
    };

    // The form of an unfinished application. "continue" skips the question the form would ask,
    // "from" brings the student back with "Wróć".
    public static string Continue(DraftSummary draft, string from) => draft.TypeCode switch
    {
        ApplicationTypeCodes.GroupChange =>
            $"new/group-change/{draft.EnrollmentId}?continue=true&from={Uri.EscapeDataString(from)}",
        _ => "new"
    };

    // Opened from the start page: "Wróć" leads back there instead of to the list
    public static string FromHome(string url) => $"{url}?from=/";

    public static bool IsFromHome(string? from) => from == "/";

    // Passes the trace on to the next page, e.g. from an application to its correction
    public static string KeepFromHome(string url, string? from) => IsFromHome(from) ? FromHome(url) : url;
}