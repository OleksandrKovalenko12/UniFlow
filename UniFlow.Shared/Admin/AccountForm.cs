using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace UniFlow.Shared.Admin;

// An account as the administrator creates or edits it. The same rules run in the browser
// and on the server.
public sealed class AccountForm : IValidatableObject
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 256;

    // The same rule as in StudentProfile (Domain)
    public const int MinAlbumNumberLength = 4;
    public const int MaxAlbumNumberLength = 10;

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    // The university e-mail - also the login
    public string Email { get; set; } = "";

    public List<string> Roles { get; set; } = [];

    // Student only
    public string? AlbumNumber { get; set; }

    // Dean's office and vice-dean only: the faculties whose applications they handle
    public List<int> FacultyIds { get; set; } = [];

    // The form filled in with the account as it is now - for editing
    public static AccountForm From(AccountDetails account) => new()
    {
        FirstName = account.FirstName,
        LastName = account.LastName,
        Email = account.Email,
        Roles = [.. account.Roles],
        AlbumNumber = account.AlbumNumber,
        FacultyIds = [.. account.FacultyIds],
    };

    public bool Has(string role) => Roles.Contains(role);

    public bool IsStaff() => Has(RoleNames.DeanOffice) || Has(RoleNames.ViceDean);

    // Trims text and drops what the chosen roles do not use - e.g. an album number typed in
    // before "Student" was unticked
    public void Normalize()
    {
        FirstName = FirstName?.Trim() ?? "";
        LastName = LastName?.Trim() ?? "";
        Email = Email?.Trim() ?? "";
        Roles = (Roles ?? []).Distinct().ToList();
        AlbumNumber = Has(RoleNames.Student) ? AlbumNumber?.Trim() : null;
        FacultyIds = IsStaff() ? (FacultyIds ?? []).Distinct().ToList() : [];
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FirstName))
            yield return Error("Podaj imię.", nameof(FirstName));
        else if (FirstName.Trim().Length > MaxNameLength)
            yield return Error($"Imię może mieć najwyżej {MaxNameLength} znaków.", nameof(FirstName));

        if (string.IsNullOrWhiteSpace(LastName))
            yield return Error("Podaj nazwisko.", nameof(LastName));
        else if (LastName.Trim().Length > MaxNameLength)
            yield return Error($"Nazwisko może mieć najwyżej {MaxNameLength} znaków.", nameof(LastName));

        var email = Email?.Trim() ?? "";
        if (email.Length == 0)
            yield return Error("Podaj e-mail.", nameof(Email));
        else if (email.Length > MaxEmailLength || !IsEmail(email))
            yield return Error("Podaj poprawny adres e-mail.", nameof(Email));

        if (Roles is null || Roles.Count == 0)
            yield return Error("Wybierz co najmniej jedną rolę.", nameof(Roles));
        else if (Roles.Any(role => !RoleNames.All.Contains(role)))
            yield return Error("Nieznana rola.", nameof(Roles));

        if (Has(RoleNames.Student))
        {
            var album = AlbumNumber?.Trim() ?? "";
            if (album.Length == 0)
                yield return Error("Podaj numer albumu.", nameof(AlbumNumber));
            else if (album.Length < MinAlbumNumberLength || album.Length > MaxAlbumNumberLength
                || !album.All(char.IsAsciiDigit))
                yield return Error($"Numer albumu to {MinAlbumNumberLength}-{MaxAlbumNumberLength} cyfr.",
                    nameof(AlbumNumber));
        }

        // Without a faculty an employee would see no applications at all
        if (IsStaff() && (FacultyIds is null || FacultyIds.Count == 0))
            yield return Error("Wybierz co najmniej jeden wydział.", nameof(FacultyIds));
    }

    private static bool IsEmail(string email) =>
        MailAddress.TryCreate(email, out var address) && address.Address == email;

    private static ValidationResult Error(string message, string field) => new(message, [field]);
}