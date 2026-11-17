namespace UniFlow.Client.Admin;

// The account list's filters survive opening an account and coming back
public sealed class AccountListState
{
    public string? Search { get; set; }

    // One of RoleNames, "" = all
    public string Role { get; set; } = "";

    // "active", "inactive", "" = all
    public string Status { get; set; } = "";
}