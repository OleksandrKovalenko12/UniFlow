using UniFlow.Client.Auth;
using UniFlow.Shared.Admin;

namespace UniFlow.Client.Admin;

// An audit entry in words, e.g. "Nadano rolę: Prodziekan"
public static class AuditTexts
{
    public static string Describe(AuditLogItem entry) => entry.Kind switch
    {
        AuditEntryKind.UserCreated => "Utworzono konto",
        AuditEntryKind.UserActivated => "Włączono konto",
        AuditEntryKind.UserDeactivated => "Wyłączono konto",
        AuditEntryKind.PasswordReset => "Zresetowano hasło",
        AuditEntryKind.LoginEmailChanged => With("Zmieniono e-mail", entry.Details),
        AuditEntryKind.PersonalDataCorrected => With("Poprawiono dane", entry.Details),
        // A role is kept as its name ("ViceDean") - shown with its Polish label
        AuditEntryKind.RoleAssigned => With("Nadano rolę", Role(entry.Details)),
        AuditEntryKind.RoleRemoved => With("Odebrano rolę", Role(entry.Details)),
        AuditEntryKind.StaffAssigned => With("Przypisano do wydziału", entry.Details),
        AuditEntryKind.StaffUnassigned => With("Usunięto z wydziału", entry.Details),
        AuditEntryKind.FacultyCreated => With("Dodano wydział", entry.Details),
        AuditEntryKind.FacultyChanged => With("Zmieniono wydział", entry.Details),
        AuditEntryKind.FieldOfStudyCreated => With("Dodano kierunek", entry.Details),
        AuditEntryKind.FieldOfStudyChanged => With("Zmieniono kierunek", entry.Details),
        AuditEntryKind.ApplicationTypeChanged => With("Zmieniono rodzaj wniosku", entry.Details),
        _ => entry.Kind.ToString()
    };

    public static string Group(AuditGroup group) => group switch
    {
        AuditGroup.Accounts => "Konta i dane",
        AuditGroup.Roles => "Role",
        AuditGroup.Faculties => "Wydziały pracowników",
        AuditGroup.Structure => "Struktura i rodzaje wniosków",
        _ => group.ToString()
    };

    private static string With(string text, string? details) =>
        string.IsNullOrEmpty(details) ? text : $"{text}: {details}";

    private static string? Role(string? role) => role is null ? null : UserTexts.Role(role);
}