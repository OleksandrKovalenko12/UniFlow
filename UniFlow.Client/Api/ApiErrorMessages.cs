using System.Net;

namespace UniFlow.Client.Api;

// The server sends a code (e.g. "PdfServiceUnavailable"), the Client decides what the user reads
public static class ApiErrorMessages
{
    public const string NoConnection =
        "Nie udało się połączyć z serwerem. Sprawdź połączenie i spróbuj ponownie.";

    public const string SessionExpired = "Sesja wygasła. Zaloguj się ponownie.";

    // Codes from the server: ErrorMapping (Api) and DomainErrorCode (Domain)
    private static readonly Dictionary<string, string> ByCode = new()
    {
        ["ConcurrencyConflict"] = "Wniosek został w międzyczasie zmieniony. Odśwież stronę.",
        ["PdfServiceUnavailable"] =
            "Usługa generowania dokumentów jest chwilowo niedostępna, spróbuj za chwilę.",
        ["EnrollmentNotFound"] = "Nie znaleziono wybranych studiów.",
        ["EnrollmentInactive"] = "Wybrane studia nie są już aktywne.",
        ["ApplicationTypeInactive"] = "Tego rodzaju wniosku nie można już złożyć."
    };

    public static string For(HttpStatusCode status, string? code, bool hasFieldErrors)
    {
        if (code is not null && ByCode.TryGetValue(code, out var message))
            return message;

        if (hasFieldErrors)
            return "Popraw zaznaczone pola formularza.";

        return status switch
        {
            HttpStatusCode.Forbidden => "Nie masz uprawnień do tej operacji.",
            HttpStatusCode.NotFound => "Nie znaleziono szukanych danych.",
            HttpStatusCode.Conflict => "Tej operacji nie można teraz wykonać. Odśwież stronę.",
            HttpStatusCode.TooManyRequests =>
                "Złożono zbyt wiele wniosków w krótkim czasie. Spróbuj ponownie później.",
            HttpStatusCode.ServiceUnavailable => "Usługa jest chwilowo niedostępna, spróbuj za chwilę.",
            _ when (int)status >= 500 => "Wystąpił błąd serwera. Spróbuj ponownie później.",
            _ => "Nie udało się wykonać operacji."
        };
    }
}