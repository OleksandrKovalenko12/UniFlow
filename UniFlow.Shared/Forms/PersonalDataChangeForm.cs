using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UniFlow.Shared.Admin;

namespace UniFlow.Shared.Forms;

// The student ticks what changes - at least one part - and fills only those parts: the old
// value and the new one. Only the current name is taken from the account (ValidateAgainst
// checks that it really changes); the address and the phone are only in USOS, so the student
// writes them. Only a new name needs a supporting document (RequiredAttachments).
public sealed partial class PersonalDataChangeForm : IValidatableObject
{
    // The same limits as on the account - the new values must fit there
    public const int MaxNameLength = AccountForm.MaxNameLength;
    public const int MaxEmailLength = AccountForm.MaxEmailLength;
    public const int MaxReasonDetailsLength = 500;

    // The error of the ticks themselves - shown above them, not under a field
    public const string ChangesField = "Changes";

    public bool ChangeName { get; set; }
    public bool ChangeAddress { get; set; }
    public bool ChangePhone { get; set; }
    public bool ChangeEmail { get; set; }

    // ChangeName - the old name is on the account
    public string? NewFirstName { get; set; }
    public string? NewLastName { get; set; }
    public PersonalDataChangeReason Reason { get; set; }
    public string? ReasonDetails { get; set; } // Reason = Other

    // ChangeAddress - the correspondence address
    public PostalAddress OldAddress { get; set; } = new();
    public PostalAddress NewAddress { get; set; } = new();

    // ChangePhone
    public string? OldPhone { get; set; }
    public string? NewPhone { get; set; }

    // ChangeEmail - the private one; the university address does not change
    public string? OldEmail { get; set; }
    public string? NewEmail { get; set; }

    // A new first name or last name is official - a document must confirm it.
    // Not saved with the form - it follows from it.
    [JsonIgnore]
    public int RequiredAttachments => ChangeName ? 1 : 0;

    // Trims text, keeps only digits and "+" of the phones, and clears the parts not ticked
    public void Normalize()
    {
        NewFirstName = ChangeName ? NewFirstName?.Trim() : null;
        NewLastName = ChangeName ? NewLastName?.Trim() : null;
        Reason = ChangeName ? Reason : default;
        ReasonDetails = ChangeName && Reason == PersonalDataChangeReason.Other ? ReasonDetails?.Trim() : null;

        if (ChangeAddress)
        {
            OldAddress.Normalize();
            NewAddress.Normalize();
        }
        else
        {
            OldAddress = new PostalAddress();
            NewAddress = new PostalAddress();
        }

        OldPhone = ChangePhone ? PhoneDigits(OldPhone) : null;
        NewPhone = ChangePhone ? PhoneDigits(NewPhone) : null;
        OldEmail = ChangeEmail ? OldEmail?.Trim() : null;
        NewEmail = ChangeEmail ? NewEmail?.Trim() : null;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ChangeName && !ChangeAddress && !ChangePhone && !ChangeEmail)
            yield return Error("Zaznacz, co chcesz zmienić.", ChangesField);

        if (ChangeName)
        {
            foreach (var error in ValidateName())
                yield return error;
        }

        if (ChangeAddress)
        {
            var errors = OldAddress.Validate($"{nameof(OldAddress)}.")
                .Concat(NewAddress.Validate($"{nameof(NewAddress)}."))
                .ToList();
            foreach (var error in errors)
                yield return error;

            if (errors.Count == 0 && NewAddress.SameAs(OldAddress))
                yield return Error("Nowy adres jest taki sam jak dotychczasowy.",
                    $"{nameof(NewAddress)}.{nameof(PostalAddress.Street)}");
        }

        if (ChangePhone)
        {
            var oldError = PhoneError(OldPhone, nameof(OldPhone));
            var newError = PhoneError(NewPhone, nameof(NewPhone));
            if (oldError is not null)
                yield return oldError;
            if (newError is not null)
                yield return newError;

            if (oldError is null && newError is null && PhoneDigits(OldPhone) == PhoneDigits(NewPhone))
                yield return Error("Nowy numer jest taki sam jak dotychczasowy.", nameof(NewPhone));
        }

        if (ChangeEmail)
        {
            var oldError = EmailError(OldEmail, nameof(OldEmail));
            var newError = EmailError(NewEmail, nameof(NewEmail));
            if (oldError is not null)
                yield return oldError;
            if (newError is not null)
                yield return newError;

            if (oldError is null && newError is null
                && string.Equals(OldEmail!.Trim(), NewEmail!.Trim(), StringComparison.OrdinalIgnoreCase))
                yield return Error("Nowy adres e-mail jest taki sam jak dotychczasowy.", nameof(NewEmail));
        }
    }

    // What only the server knows: the current name on the account. A new name must differ.
    // Exact comparison - a fixed capital letter is a change too.
    public IEnumerable<ValidationResult> ValidateAgainst(CurrentPersonalData current)
    {
        if (ChangeName
            && string.Equals(NewFirstName?.Trim(), current.FirstName, StringComparison.Ordinal)
            && string.Equals(NewLastName?.Trim(), current.LastName, StringComparison.Ordinal))
            yield return Error("Imię i nazwisko są takie same jak obecne - zmień przynajmniej jedno z nich.",
                nameof(NewLastName));
    }

    private IEnumerable<ValidationResult> ValidateName()
    {
        if (string.IsNullOrWhiteSpace(NewFirstName))
            yield return Error("Podaj imię.", nameof(NewFirstName));
        else if (NewFirstName.Trim().Length > MaxNameLength)
            yield return Error($"Imię może mieć najwyżej {MaxNameLength} znaków.", nameof(NewFirstName));

        if (string.IsNullOrWhiteSpace(NewLastName))
            yield return Error("Podaj nazwisko.", nameof(NewLastName));
        else if (NewLastName.Trim().Length > MaxNameLength)
            yield return Error($"Nazwisko może mieć najwyżej {MaxNameLength} znaków.", nameof(NewLastName));

        if (!Enum.IsDefined(Reason))
            yield return Error("Wybierz powód zmiany.", nameof(Reason));
        else if (Reason == PersonalDataChangeReason.Other)
        {
            if (string.IsNullOrWhiteSpace(ReasonDetails))
                yield return Error("Opisz powód zmiany.", nameof(ReasonDetails));
            else if (ReasonDetails.Trim().Length > MaxReasonDetailsLength)
                yield return Error($"Opis może mieć najwyżej {MaxReasonDetailsLength} znaków.",
                    nameof(ReasonDetails));
        }
    }

    private static ValidationResult? PhoneError(string? phone, string field)
    {
        var digits = PhoneDigits(phone);
        if (digits.Length == 0)
            return Error("Podaj numer telefonu.", field);
        if (!PhoneFormat().IsMatch(digits))
            return Error("Podaj numer telefonu, np. 600 123 456 albo +48 600 123 456.", field);
        return null;
    }

    private static ValidationResult? EmailError(string? email, string field)
    {
        var trimmed = email?.Trim() ?? "";
        if (trimmed.Length == 0)
            return Error("Podaj adres e-mail.", field);
        if (trimmed.Length > MaxEmailLength || !new EmailAddressAttribute().IsValid(trimmed))
            return Error("Podaj poprawny adres e-mail.", field);
        return null;
    }

    // "600 123 456", "600-123-456", "(+48) 600..." -> "600123456", "+48600..."
    private static string PhoneDigits(string? phone) => PhoneSeparators().Replace(phone ?? "", "");

    // 9 to 15 digits, "+" in front for a number from abroad
    [GeneratedRegex(@"^\+?\d{9,15}$")]
    private static partial Regex PhoneFormat();

    // What people type around the digits
    [GeneratedRegex(@"[\s\-()]")]
    private static partial Regex PhoneSeparators();

    private static ValidationResult Error(string message, string memberName) =>
        new(message, [memberName]);
}