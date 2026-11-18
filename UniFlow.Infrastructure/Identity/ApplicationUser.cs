using Microsoft.AspNetCore.Identity;

namespace UniFlow.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    // A temporary password given by the administrator - the user sets their own at the next login
    public bool MustChangePassword { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PreviousLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }
}