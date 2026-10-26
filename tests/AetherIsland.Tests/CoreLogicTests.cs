using AetherIsland.Core.Logic;
using Xunit;

namespace AetherIsland.Tests;

public class DayProgressTests
{
    [Fact]
    public void DayFraction_IsZero_BeforeWake()
    {
        var now = DateTime.Today.AddHours(5);
        Assert.Equal(0d, DayProgressCalculator.DayFraction(now, new TimeSpan(7, 0, 0), new TimeSpan(23, 0, 0)), 5);
    }

    [Fact]
    public void DayFraction_IsOne_AfterBed()
    {
        var now = DateTime.Today.AddHours(23).AddMinutes(30);
        Assert.Equal(1d, DayProgressCalculator.DayFraction(now, new TimeSpan(7, 0, 0), new TimeSpan(23, 0, 0)), 5);
    }

    [Fact]
    public void DayFraction_IsHalf_Midday()
    {
        // wake 06:00, bed 22:00 → noon = 6/16 = 0.375
        var now = DateTime.Today.AddHours(12);
        Assert.Equal(0.375, DayProgressCalculator.DayFraction(now, new TimeSpan(6, 0, 0), new TimeSpan(22, 0, 0)), 4);
    }

    [Fact]
    public void DayFraction_HandlesPastMidnightBed()
    {
        var now = DateTime.Today.AddHours(23);
        // bed 01:00 next day → treated as 25:00 → (23-7)/(25-7) = 16/18
        Assert.Equal(16.0 / 18, DayProgressCalculator.DayFraction(now, new TimeSpan(7, 0, 0), new TimeSpan(1, 0, 0)), 4);
    }

    [Fact]
    public void WorkFraction_IsNull_OutsideWorkday()
    {
        var now = DateTime.Today.AddHours(20);
        Assert.Null(DayProgressCalculator.WorkFraction(now, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)));
    }

    [Fact]
    public void RemainingUntil_CrossesMidnight()
    {
        var now = DateTime.Today.AddHours(23); // 23:00
        var remaining = DayProgressCalculator.RemainingUntil(now, "01:00");
        Assert.Equal(2d, remaining.TotalHours, 5);
    }

    [Fact]
    public void ParseTime_FallsBack_OnGarbage()
    {
        var fallback = new TimeSpan(23, 30, 0);
        Assert.Equal(fallback, DayProgressCalculator.ParseTime("not-a-time", fallback));
    }
}

public class QuotaMathTests
{
    [Fact]
    public void PercentUsed_IsNull_WithoutLimit()
    {
        Assert.Null(QuotaMath.PercentUsed(100, null));
        Assert.Null(QuotaMath.PercentUsed(100, 0));
    }

    [Fact]
    public void PercentUsed_ComputesAndClamps()
    {
        Assert.Equal(50d, QuotaMath.PercentUsed(5, 10)!.Value, 3);
        Assert.Equal(999d, QuotaMath.PercentUsed(50, 1)!.Value, 3);
    }

    [Fact]
    public void Remaining_NeverNegative()
    {
        Assert.Equal(5d, QuotaMath.Remaining(10, 5));
        Assert.Equal(0d, QuotaMath.Remaining(10, 20));
        Assert.Null(QuotaMath.Remaining(null, 5));
    }

    [Fact]
    public void CrossedThreshold_PicksHighestCrossed()
    {
        var thresholds = new[] { 50, 80, 90, 100 };
        Assert.Null(QuotaMath.CrossedThreshold(null, thresholds));
        Assert.Null(QuotaMath.CrossedThreshold(49.9, thresholds));
        Assert.Equal(80, QuotaMath.CrossedThreshold(82, thresholds));
        Assert.Equal(100, QuotaMath.CrossedThreshold(100, thresholds));
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData(999L, "999")]
    [InlineData(1500L, "1.5K")]
    [InlineData(2_500_000L, "2.5M")]
    [InlineData(1_200_000_000L, "1.2B")]
    public void FormatTokens_Humanizes(long? input, string expected)
    {
        Assert.Equal(expected, QuotaMath.FormatTokens(input));
    }
}
