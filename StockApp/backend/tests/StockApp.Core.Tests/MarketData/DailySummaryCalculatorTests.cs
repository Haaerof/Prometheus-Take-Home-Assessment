using System.Globalization;
using StockApp.Core.MarketData.Models;
using StockApp.Core.MarketData.Services;

namespace StockApp.Core.Tests.MarketData;

public class DailySummaryCalculatorTests
{
    private const string NewYork = "America/New_York";
    private const string Auckland = "Pacific/Auckland";

    private readonly DailySummaryCalculator _calculator = new();

    [Fact]
    public void AveragesLowsAndHighs_AndSumsVolume_ForOneDay()
    {
        var series = SeriesIn(NewYork,
            BarAt("2026-09-14T13:30:00Z", low: 100m, high: 110m, volume: 1_000),
            BarAt("2026-09-14T13:45:00Z", low: 102m, high: 114m, volume: 2_500));

        var summary = Assert.Single(_calculator.Summarise(series));

        Assert.Equal(new DateOnly(2026, 9, 14), summary.Day);
        Assert.Equal(101m, summary.LowAverage);
        Assert.Equal(112m, summary.HighAverage);
        Assert.Equal(3_500L, summary.Volume);
    }

    [Fact]
    public void ReturnsOneSummaryPerDay_OldestFirst_RegardlessOfInputOrder()
    {
        var series = SeriesIn(NewYork,
            BarAt("2026-09-16T13:30:00Z", low: 3m, high: 3m, volume: 1),
            BarAt("2026-09-14T13:30:00Z", low: 1m, high: 1m, volume: 1),
            BarAt("2026-09-15T13:30:00Z", low: 2m, high: 2m, volume: 1));

        var summaries = _calculator.Summarise(series);

        Assert.Equal(
            [new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 16)],
            summaries.Select(summary => summary.Day));
    }

    /// <summary>
    /// The Auckland session opens at 10:00 local, which is 22:00 UTC on the *previous* date.
    /// Grouping on the UTC date would split one trading session into two partial days.
    /// </summary>
    [Fact]
    public void GroupsOnTheExchangeDate_NotTheUtcDate()
    {
        var series = SeriesIn(Auckland,
            BarAt("2026-09-13T22:00:00Z", low: 10m, high: 12m, volume: 100),   // 14 Sep, 10:00 local
            BarAt("2026-09-14T02:00:00Z", low: 14m, high: 16m, volume: 300));  // 14 Sep, 14:00 local

        var summary = Assert.Single(_calculator.Summarise(series));

        Assert.Equal(new DateOnly(2026, 9, 14), summary.Day);
        Assert.Equal(12m, summary.LowAverage);
        Assert.Equal(400L, summary.Volume);
    }

    /// <summary>
    /// Daylight saving ends in New York at 02:00 local on 1 November 2026, which is 06:00 UTC.
    /// Both bars below fall on 1 November locally, but they sit on different UTC offsets.
    /// Reusing one offset for the whole range — the trap of taking the response's single
    /// <c>gmtoffset</c> field — would convert the first bar to 23:30 on 31 October and split
    /// the session across two days.
    /// </summary>
    [Fact]
    public void UsesTheOffsetInForceAtEachInstant_AcrossADaylightSavingChange()
    {
        var series = SeriesIn(NewYork,
            BarAt("2026-11-01T04:30:00Z", low: 1m, high: 1m, volume: 1),   // 1 Nov, 00:30 EDT (-4)
            BarAt("2026-11-01T07:30:00Z", low: 3m, high: 3m, volume: 1));  // 1 Nov, 02:30 EST (-5)

        var summary = Assert.Single(_calculator.Summarise(series));

        Assert.Equal(new DateOnly(2026, 11, 1), summary.Day);
        Assert.Equal(2m, summary.LowAverage);
    }

    [Fact]
    public void ReturnsNothingForAnEmptySeries()
    {
        Assert.Empty(_calculator.Summarise(SeriesIn(NewYork)));
    }

    /// <summary>
    /// Rounding belongs to the published contract, not the calculation, so the average keeps
    /// every digit it has. A <c>double</c> here would already have introduced binary error.
    /// </summary>
    [Fact]
    public void KeepsFullPrecision_LeavingRoundingToTheCaller()
    {
        var series = SeriesIn(NewYork,
            BarAt("2026-09-14T13:30:00Z", low: 1m, high: 1m, volume: 1),
            BarAt("2026-09-14T13:45:00Z", low: 2m, high: 2m, volume: 1),
            BarAt("2026-09-14T14:00:00Z", low: 2m, high: 2m, volume: 1));

        var summary = Assert.Single(_calculator.Summarise(series));

        Assert.Equal(5m / 3m, summary.LowAverage);
    }

    /// <summary>A day's volume can exceed <see cref="int.MaxValue"/>, so the total is a 64-bit integer.</summary>
    [Fact]
    public void SumsVolumeWithoutOverflowing32Bits()
    {
        var series = SeriesIn(NewYork,
            BarAt("2026-09-14T13:30:00Z", low: 1m, high: 1m, volume: 2_000_000_000L),
            BarAt("2026-09-14T13:45:00Z", low: 1m, high: 1m, volume: 2_000_000_000L));

        var summary = Assert.Single(_calculator.Summarise(series));

        Assert.Equal(4_000_000_000L, summary.Volume);
    }

    private static IntradaySeries SeriesIn(string timeZoneId, params IntradayBar[] bars) =>
        new(Symbol.Create("TSLA"),
            ExchangeTimeZone.Resolved(timeZoneId, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)),
            bars);

    private static IntradayBar BarAt(string utcTimestamp, decimal low, decimal high, long volume) =>
        new(DateTimeOffset.Parse(utcTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            low, high, volume);
}
