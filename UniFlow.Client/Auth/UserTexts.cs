using UniFlow.Shared;

namespace UniFlow.Client.Auth;

// How a user and their roles are shown in the app bar and in the profile
public static class UserTexts
{
    // "Jan Zieliński" -> "JZ"
    public static string Initials(string? name) =>
        string.Concat((name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpper(part[0])));

    public static string Role(string role) => role switch
    {
        RoleNames.Student => "Student",
        RoleNames.DeanOffice => "Pracownik dziekanatu",
        RoleNames.ViceDean => "Prodziekan",
        RoleNames.Administrator => "Administrator",
        _ => role
    };
}