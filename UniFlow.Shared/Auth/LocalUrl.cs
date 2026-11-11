namespace UniFlow.Shared.Auth;

// Checks a return address before redirecting to it after login.
// Only paths inside this application are accepted, otherwise a link like
// /login?returnUrl=https://another.pl could send the user to a fake site (open redirect).
public static class LocalUrl
{
    public static bool IsLocal(string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        // "//another.pl" and "/\obcy.pl" are read by browsers as addresses of another site
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
        // Browsers drop characters like tab, so "/\t/another.pl" would become "//obcy.pl"
        && !url.Any(char.IsControl);
}