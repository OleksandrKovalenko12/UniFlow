using UniFlow.Infrastructure.Numbering;

namespace UniFlow.Tests.Infrastructure;

public class ApplicationNumberFormatTests
{
    [Theory]
    [InlineData(2026, 1, "UF/2026/0001")]
    [InlineData(2026, 42, "UF/2026/0042")]
    [InlineData(2026, 9999, "UF/2026/9999")]
    [InlineData(2026, 12345, "UF/2026/12345")]
    public void Format_ValidValues_ReturnsNumber(int year, int sequence, string expected)
    {
        Assert.Equal(expected, ApplicationNumberFormat.Format(year, sequence));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Format_SequenceBelowOne_Throws(int sequence)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => ApplicationNumberFormat.Format(2026, sequence));

        Assert.Equal("sequence", ex.ParamName);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(10000)]
    public void Format_YearOutOfRange_Throws(int year)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => ApplicationNumberFormat.Format(year, 1));

        Assert.Equal("year", ex.ParamName);
    }
}