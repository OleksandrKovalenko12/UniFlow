using UniFlow.Infrastructure.Time;

namespace UniFlow.Tests.Infrastructure;

public class PolishTimeTests
{
    [Fact]
    public void FromUtc_Winter_AddsOneHour()
    {
        var result = PolishTime.FromUtc(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 1, 15, 11, 0, 0), result);
    }

    [Fact]
    public void FromUtc_Summer_AddsTwoHours()
    {
        var result = PolishTime.FromUtc(new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 7, 15, 12, 0, 0), result);
    }

    [Fact]
    public void FromUtc_NewYearEve_IsAlreadyNextYear()
    {
        var result = PolishTime.FromUtc(new DateTime(2026, 12, 31, 23, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2027, 1, 1, 0, 30, 0), result);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void FromUtc_NotUtc_Throws(DateTimeKind kind)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => PolishTime.FromUtc(new DateTime(2026, 1, 15, 10, 0, 0, kind)));

        Assert.Equal("utc", ex.ParamName);
    }
}