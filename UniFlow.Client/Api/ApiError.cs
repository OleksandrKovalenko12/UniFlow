namespace UniFlow.Client.Api;

// What went wrong, ready to show: a Polish message for the whole page and,
// for an invalid form, messages per field (key = path like "Subjects[0].RequestedGroup")
public sealed record ApiError(
    string? Code,
    string Message,
    IReadOnlyDictionary<string, string[]> FieldErrors);