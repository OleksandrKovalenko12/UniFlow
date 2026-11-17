using System.ComponentModel.DataAnnotations;

namespace UniFlow.Shared.Auth;

// What a password must contain - the server sets Identity up with the same length,
// the page shows the hint
public static class PasswordRules
{
    public const int MinLength = 8;

    public const string Hint =
        "Co najmniej 8 znaków, w tym mała i wielka litera, cyfra i znak specjalny (np. ! lub #).";
}

// Setting one's own password in place of the temporary one
public sealed class ChangePasswordRequest : IValidatableObject
{
    [Required(ErrorMessage = "Podaj hasło tymczasowe.")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Podaj nowe hasło.")]
    [MinLength(PasswordRules.MinLength, ErrorMessage = "Hasło musi mieć co najmniej 8 znaków.")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Powtórz nowe hasło.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Hasła nie są takie same.")]
    public string ConfirmPassword { get; set; } = "";

    // The rest of the rules (letters, digit, special character) are checked by Identity
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NewPassword.Length > 0 && NewPassword == CurrentPassword)
            yield return new ValidationResult("Nowe hasło musi się różnić od tymczasowego.",
                [nameof(NewPassword)]);
    }
}