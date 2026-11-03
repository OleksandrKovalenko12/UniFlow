using System.ComponentModel.DataAnnotations;

namespace UniFlow.Shared.Auth;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Podaj e-mail.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Podaj hasło.")]
    public string Password { get; set; } = "";
}