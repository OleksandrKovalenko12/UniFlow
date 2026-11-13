using System.Text.Json.Serialization;
using UniFlow.Shared.Applications;

namespace UniFlow.Shared.Review;

// Tabs of the dean's office list - each one is a group of statuses
[JsonConverter(typeof(JsonStringEnumConverter<ReviewTab>))]
public enum ReviewTab
{
    New,                // Submitted, Resubmitted
    InReview,           // UnderReview
    WaitingForStudent,  // NeedsCorrection
    AtViceDean,         // ForwardedToDean
    Closed              // Approved, Rejected, Withdrawn
}

// What an employee may do with an application now - the server decides, the page shows buttons
[JsonConverter(typeof(JsonStringEnumConverter<ReviewAction>))]
public enum ReviewAction
{
    Take,
    RequestCorrection,
    ForwardToDean,
    Approve,
    Reject,
    ReturnToDeanOffice
}

public sealed record ReviewListItem(
    Guid Id,
    string Number,
    string TypeName,
    string StudentName,
    string AlbumNumber,
    string FieldOfStudyName,
    string StudyLevel,
    string FacultyCode,
    ApplicationState Status,
    DateTime SubmittedAt,
    string? AssignedToName,
    bool AssignedToMe);

// An employee of the dean's office of my faculties - for the "Prowadzi" filter
public sealed record StaffMember(string Id, string Name);

// The "Prowadzi" filter: Me and None are fixed values, anything else is an employee's id
public static class AssignedFilter
{
    public const string Me = "me";
    public const string None = "none";
}

// Numbers next to the tab names
public sealed record ReviewCounts(int New, int InReview, int WaitingForStudent, int AtViceDean,
    int Closed);

public sealed record PagedList<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);