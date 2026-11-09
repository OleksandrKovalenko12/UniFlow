using UniFlow.Api.Texts;
using UniFlow.Domain.Enums;

namespace UniFlow.Tests.Api;

public class StudyTextsTests
{
    // Goes through every enum value - a new value without a text fails here, not in production
    [Fact]
    public void Level_EveryValue_HasText()
    {
        foreach (var level in Enum.GetValues<StudyLevel>())
            Assert.False(string.IsNullOrWhiteSpace(StudyTexts.Level(level)));
    }

    [Fact]
    public void Mode_EveryValue_HasText()
    {
        foreach (var mode in Enum.GetValues<StudyMode>())
            Assert.False(string.IsNullOrWhiteSpace(StudyTexts.Mode(mode)));
    }
}