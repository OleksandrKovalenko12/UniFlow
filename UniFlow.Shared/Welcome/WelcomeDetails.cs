using UniFlow.Shared.Admin;
using UniFlow.Shared.Applications;

namespace UniFlow.Shared.Welcome;

// The start page after logging in. PreviousLoginAt: null at the very first login.
// Each section is there only for a user with that role - a student working in the dean's
// office gets both.
public sealed record WelcomeDetails(
    string FirstName,
    DateTime? PreviousLoginAt,
    StudentWelcome? Student,
    DeanOfficeWelcome? DeanOffice,
    ViceDeanWelcome? ViceDean,
    AdminWelcome? Admin);

// Changes: what the dean's office or the vice-dean did with the student's applications since
// the previous login - the student's own actions are not news to them.
// Recent: the latest applications with their status now - shown when there are no changes.
public sealed record StudentWelcome(
    int InProgress,
    int NeedsCorrection,
    int Drafts,
    IReadOnlyList<WelcomeChange> Changes,
    IReadOnlyList<WelcomeChange> Recent);

// MyCorrected: cases the employee sent back, now corrected by the student - waiting for them.
// OldestNew: the new applications that have waited longest - the front of the queue.
// The rest: the faculties as a whole.
public sealed record DeanOfficeWelcome(
    IReadOnlyList<WelcomeChange> MyCorrected,
    int MyInReview,
    IReadOnlyList<WelcomeChange> OldestNew,
    int New,
    int NewSinceLastLogin,
    int OthersCorrected,
    int WaitingForStudent);

public sealed record ViceDeanWelcome(int Waiting, int WaitingSinceLastLogin);

public sealed record AdminWelcome(
    int ActiveAccounts,
    int InactiveAccounts,
    int AwaitingPassword,
    IReadOnlyList<AuditLogItem> RecentChanges);

// One application and its status. ChangedAt: when it happened - the change, or the
// submission for "Ostatnie wnioski" and "Najdłużej czekające".
public sealed record WelcomeChange(
    Guid ApplicationId,
    string Number,
    string TypeName,
    ApplicationState Status,
    DateTime ChangedAt);