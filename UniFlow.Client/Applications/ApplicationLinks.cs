using UniFlow.Shared.ApplicationTypes;
using UniFlow.Shared.Drafts;

namespace UniFlow.Client.Applications;

// Where each kind of application has its form - one place for every page that links to them
public static class ApplicationLinks
{
    // The end of the addresses of each kind - the same on the pages and in the API.
    // A kind without a form here is not offered to the student.
    private static readonly Dictionary<string, string> FormRoutes = new()
    {
        [ApplicationTypeCodes.GroupChange] = "group-change",
        [ApplicationTypeCodes.PersonalDataChange] = "personal-data-change"
    };

    public static bool HasForm(string typeCode) => FormRoutes.ContainsKey(typeCode);

    public static string FormRoute(string typeCode) => FormRoutes[typeCode];

    // The form of a new application on given studies; "from" brings the student back with "Wróć"
    public static string New(string typeCode, int enrollmentId, string? from = null) =>
        $"new/{FormRoutes[typeCode]}/{enrollmentId}" +
        (from is null ? "" : $"?from={Uri.EscapeDataString(from)}");

    // The correction page of an application sent back; null for a kind without one
    public static string? Correction(string typeCode, Guid applicationId) =>
        FormRoutes.TryGetValue(typeCode, out var route)
            ? $"applications/{applicationId}/correct/{route}"
            : null;

    // The form of an unfinished application. "continue" skips the question the form would ask,
    // "from" brings the student back with "Wróć".
    public static string Continue(DraftSummary draft, string from) =>
        FormRoutes.TryGetValue(draft.TypeCode, out var route)
            ? $"new/{route}/{draft.EnrollmentId}?continue=true&from={Uri.EscapeDataString(from)}"
            : "new";

    // Opened from the start page: "Wróć" leads back there instead of to the list
    public static string FromHome(string url) => $"{url}?from=/";

    public static bool IsFromHome(string? from) => from == "/";

    // Passes the trace on to the next page, e.g. from an application to its correction
    public static string KeepFromHome(string url, string? from) => IsFromHome(from) ? FromHome(url) : url;
}