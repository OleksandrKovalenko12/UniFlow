using UniFlow.Shared.Admin;
using UniFlow.Shared.Applications;
using UniFlow.Shared.Drafts;

namespace UniFlow.Shared.Welcome;

// The start page after logging in: what is waiting for the user and what has changed since the
// previous login (PreviousLoginAt - null at the very first login). Each section is there only
// for a user with that role - a student working in the dean's office gets both.
public sealed record WelcomeDetails(
    string FirstName,
    DateTime? PreviousLoginAt,
    StudentWelcome? Student,
    DeanOfficeWelcome? DeanOffice,
    ViceDeanWelcome? ViceDean,
    AdminWelcome? Admin);

// The first few of a list and how many there are in all - "i jeszcze 3" under it
public sealed record WelcomeList<T>(IReadOnlyList<T> Items, int Total);

// One application in a list. StudentName: for staff only. At: when it happened - the change,
// the submission or the moment it reached the user. Comment: what the office wrote to the
// student, e.g. why it was sent back.
public sealed record WelcomeItem(
    Guid ApplicationId,
    string Number,
    string TypeCode,
    string TypeName,
    string? StudentName,
    ApplicationState Status,
    DateTime At,
    string? Comment);

// Changes: what the dean's office or the vice-dean did since the previous login - the
// student's own actions are not news to them. ToCorrect: sent back and waiting for the student.
public sealed record StudentWelcome(
    WelcomeList<WelcomeItem> Changes,
    WelcomeList<WelcomeItem> ToCorrect,
    WelcomeList<DraftSummary> Drafts,
    int InProgress,
    int Closed);

// ForYou: what only this employee can move on - corrections of cases they sent back, then
// cases they took and have not finished. NewToTake: the oldest new ones, nobody handles them.
public sealed record DeanOfficeWelcome(
    WelcomeList<WelcomeItem> ForYou,
    WelcomeList<WelcomeItem> NewToTake,
    int WaitingForStudent,
    int AtViceDean,
    int HandledByOthers);

// Forwarded and waiting for the vice-dean's decision, the longest waiting first
public sealed record ViceDeanWelcome(WelcomeList<WelcomeItem> Waiting);

// AwaitingPassword: accounts whose users have not logged in yet - someone to remind
public sealed record AdminWelcome(
    int ActiveAccounts,
    int InactiveAccounts,
    WelcomeList<AccountOption> AwaitingPassword,
    IReadOnlyList<AuditLogItem> RecentChanges);

public sealed record AccountOption(string Id, string Name);