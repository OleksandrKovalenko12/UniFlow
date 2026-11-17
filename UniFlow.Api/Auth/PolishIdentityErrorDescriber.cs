using Microsoft.AspNetCore.Identity;

namespace UniFlow.Api.Auth;

// Identity's messages about passwords, in Polish - they go straight under the password field.
// Only the ones a user can see; the rest stay in English for the logs.
public sealed class PolishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Nieprawidłowe hasło." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Hasło musi mieć co najmniej {length} znaków." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Hasło musi zawierać znak specjalny, np. ! lub #." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Hasło musi zawierać cyfrę." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Hasło musi zawierać małą literę." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "Hasło musi zawierać wielką literę." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Hasło musi zawierać co najmniej {uniqueChars} różne znaki." };
}