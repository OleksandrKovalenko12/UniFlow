namespace UniFlow.Shared.Forms;

public static class PersonalDataChangeReasonText
{
    // Drop-down list, the PDF ("Powód zmiany: zawarcie związku małżeńskiego") and the summary
    public static string Name(PersonalDataChangeReason reason) => reason switch
    {
        PersonalDataChangeReason.Marriage => "zawarcie związku małżeńskiego",
        PersonalDataChangeReason.Divorce => "rozwiązanie związku małżeńskiego",
        PersonalDataChangeReason.NameChangeDecision => "decyzja o zmianie imienia lub nazwiska",
        PersonalDataChangeReason.Other => "inny",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown reason.")
    };
}