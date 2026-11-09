using UniFlow.Domain.Enums;

namespace UniFlow.Api.Texts;

// Polish texts for study enums - one place for screens and PDF documents
public static class StudyTexts
{
    public static string Level(StudyLevel level) => level switch
    {
        StudyLevel.FirstCycle => "studia pierwszego stopnia",
        StudyLevel.SecondCycle => "studia drugiego stopnia",
        StudyLevel.LongCycle => "jednolite studia magisterskie",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown study level.")
    };

    public static string Mode(StudyMode mode) => mode switch
    {
        StudyMode.FullTime => "stacjonarne",
        StudyMode.PartTime => "niestacjonarne",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown study mode.")
    };
}