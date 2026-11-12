using MudBlazor;
using UniFlow.Shared.Review;

namespace UniFlow.Client.Applications;

public enum CommentKind
{
    None,
    Optional,
    Required
}

// Everything the screen says about one decision: the button, the dialog and the message after it
public sealed record DecisionText(
    string Button,
    Color Color,
    bool Filled,
    string Title,
    string Text,
    CommentKind Comment,
    string CommentLabel,
    string Confirm,
    string Done);

public static class DecisionTexts
{
    public static DecisionText For(ReviewAction action) => action switch
    {
        ReviewAction.Take => new("Weź do weryfikacji", Color.Primary, true,
            "", "", CommentKind.None, "", "", "Wniosek przyjęty do weryfikacji."),

        ReviewAction.RequestCorrection => new("Odeślij do poprawy", Color.Warning, false,
            "Odesłać do poprawy?",
            "Napisz, co student ma poprawić. Zobaczy ten komentarz.",
            CommentKind.Required, "Co trzeba poprawić", "Odeślij", "Wniosek odesłany do poprawy."),

        ReviewAction.ForwardToDean => new("Przekaż prodziekanowi", Color.Primary, true,
            "Przekazać prodziekanowi?",
            "Możesz dodać notatkę dla prodziekana. Student jej nie zobaczy.",
            CommentKind.Optional, "Notatka wewnętrzna (opcjonalnie)", "Przekaż",
            "Wniosek przekazany prodziekanowi."),

        ReviewAction.Approve => new("Zatwierdź", Color.Success, true,
            "Zatwierdzić wniosek?",
            "Zatwierdzenie kończy sprawę i nie można go cofnąć.",
            CommentKind.None, "", "Zatwierdź", "Wniosek zatwierdzony."),

        ReviewAction.Reject => new("Odrzuć", Color.Error, false,
            "Odrzucić wniosek?",
            "Napisz uzasadnienie. Student je zobaczy. Odrzucenia nie można cofnąć.",
            CommentKind.Required, "Uzasadnienie", "Odrzuć", "Wniosek odrzucony."),

        ReviewAction.ReturnToDeanOffice => new("Zwróć do dziekanatu", Color.Default, false,
            "Zwrócić do dziekanatu?",
            "Napisz, co dziekanat ma sprawdzić. To notatka wewnętrzna - student jej nie zobaczy.",
            CommentKind.Required, "Notatka dla dziekanatu", "Zwróć", "Wniosek zwrócony do dziekanatu."),

        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown action.")
    };
}