using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using UniFlow.Shared.Admin;

namespace UniFlow.Shared.Forms;

// The student ticks what changes - at least one part - and fills only those parts. The current
// name and e-mail are on the account: ValidateAgainst checks that they really change. Only a
// new name needs a supporting document (RequiredAttachments) - a file, not a field.
public sealed partial class PersonalDataChangeForm : IValidatableObject
{
    // The same limits as on the account - the new values must fit there
    public const int MaxNameLength = AccountForm.MaxNameLength;
    public const int MaxEmailLength = AccountForm.MaxEmailLength;
    public const int MaxReasonDetailsLength = 500;
    public const int MaxStreetLength = 150;
    public const int MaxCityLength = 100;

    // The error of the ticks themselves - shown above them, not under a field
    public const string ChangesField = "Changes";

    public bool ChangeName { get; set; }
    public bool ChangeAddress { get; set; }
    public bool ChangePhone { get; set; }
    public bool ChangeEmail { get; set; }

    // ChangeName
    public string? NewFirstName { get; set; }
    public string? NewLastName { get; set; }
    public PersonalDataChangeReason Reason { get; set; }
    public string? ReasonDetails { get; set; } // Reason = Other

    // ChangeAddress - the correspondence address, in Poland
    public string? Street { get; set; } // with the number, e.g. "ul. Kwiatowa 5/12"
    public string? PostalCode { get; set; }
    public string? City { get; set; }

    // ChangePhone
    public string? Phone { get; set; }

    // ChangeEmail - the private one; the university address does not change
    public string? Email { get; set; }

    // A new first name or last name is official - a document must confirm it
    public int RequiredAttachments => ChangeName ? 1 : 0;

    // Trims text, keeps only digits and "+" of the phone, and clears the parts not ticked
    public void Normalize()
    {
        NewFirstName = ChangeName ? NewFirstName?.Trim() : null;
        NewLastName = ChangeName ? NewLastName?.Trim() : null;
        Reason = ChangeName ? Reason : default;
        ReasonDetails = ChangeName && Reason == PersonalDataChangeReason.Other ? ReasonDetails?.Trim() : null;

        Street = ChangeAddress ? Street?.Trim() : null;
        PostalCode = ChangeAddress ? PostalCode?.Trim() : null;
        City = ChangeAddress ? City?.Trim() : null;

        Phone = ChangePhone ? PhoneSeparators().Replace(Phone ?? "", "") : null;
        Email = ChangeEmail ? Email?.Trim() : null;
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
            if (string.IsNullOrWhiteSpace(Street))
                yield return Error("Podaj ulicę i numer.", nameof(Street));
            else if (Street.Trim().Length > MaxStreetLength)
                yield return Error($"Adres może mieć najwyżej {MaxStreetLength} znaków.", nameof(Street));

            if (string.IsNullOrWhiteSpace(PostalCode))
                yield return Error("Podaj kod pocztowy.", nameof(PostalCode));
            else if (!PostalCodeFormat().IsMatch(PostalCode.Trim()))
                yield return Error("Podaj kod pocztowy w formacie 00-000.", nameof(PostalCode));

            if (string.IsNullOrWhiteSpace(City))
                yield return Error("Podaj miejscowość.", nameof(City));
            else if (City.Trim().Length > MaxCityLength)
                yield return Error($"Miejscowość może mieć najwyżej {MaxCityLength} znaków.", nameof(City));
        }

        if (ChangePhone)
        {
            var phone = PhoneSeparators().Replace(Phone ?? "", "");
            if (phone.Length == 0)
                yield return Error("Podaj numer telefonu.", nameof(Phone));
            else if (!PhoneFormat().IsMatch(phone))
                yield return Error("Podaj numer telefonu, np. 600 123 456 albo +48 600 123 456.", nameof(Phone));
        }

        if (ChangeEmail)
        {
            var email = Email?.Trim() ?? "";
            if (email.Length == 0)
                yield return Error("Podaj adres e-mail.", nameof(Email));
            else if (email.Length > MaxEmailLength || !new EmailAddressAttribute().IsValid(email))
                yield return Error("Podaj poprawny adres e-mail.", nameof(Email));
        }
    }

    // What only the server knows: the current name and e-mail. Something ticked must change.
    // Exact comparison of the name - a fixed capital letter is a change too.
    public IEnumerable<ValidationResult> ValidateAgainst(CurrentPersonalData current)
    {
        if (ChangeName
            && string.Equals(NewFirstName?.Trim(), current.FirstName, StringComparison.Ordinal)
            && string.Equals(NewLastName?.Trim(), current.LastName, StringComparison.Ordinal))
            yield return Error("Imię i nazwisko są takie same jak obecne - zmień przynajmniej jedno z nich.",
                nameof(NewLastName));

        if (ChangeEmail && string.Equals(Email?.Trim(), current.PrivateEmail, StringComparison.OrdinalIgnoreCase))
            yield return Error("To jest Twój obecny adres e-mail.", nameof(Email));
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

    // "00-000"
    [GeneratedRegex(@"^\d{2}-\d{3}$")]
    private static partial Regex PostalCodeFormat();

    // 9 to 15 digits, "+" in front for a number from abroad
    [GeneratedRegex(@"^\+?\d{9,15}$")]
    private static partial Regex PhoneFormat();

    // "600 123 456", "600-123-456", "(+48) 600..." - what people type around the digits
    [GeneratedRegex(@"[\s\-()]")]
    private static partial Regex PhoneSeparators();

    private static ValidationResult Error(string message, string memberName) =>
        new(message, [memberName]);
}