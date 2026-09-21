using StockApp.Core.MarketData.Abstractions;
using StockApp.Core.MarketData.Models;

namespace StockApp.Core.MarketData.Services;

/// <inheritdoc cref="IDailySummaryCalculator" />
public sealed class DailySummaryCalculator : IDailySummaryCalculator
{
    /// <inheritdoc />
    public IReadOnlyList<DailySummary> Summarise(IntradaySeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        return series.Bars
            .GroupBy(bar => ExchangeDateOf(bar, series.ExchangeTimeZone.TimeZone))
            .OrderBy(day => day.Key)
            .Select(day => new DailySummary(
                Day: day.Key,
                LowAverage: day.Average(bar => bar.Low),
                HighAverage: day.Average(bar => bar.High),
                Volume: day.Sum(bar => bar.Volume)))
            .ToList();
    }

    /// <summary>
    /// Converts a bar's absolute timestamp to the exchange's local clock and takes the date part.
    /// </summary>
    /// <remarks>
    /// <see cref="TimeZoneInfo.ConvertTime(DateTimeOffset, TimeZoneInfo)"/> applies the offset that was
    /// in force at that instant, so a range spanning a daylight-saving change is still converted correctly.
    /// A single fixed offset would not be, which is why the time zone is carried rather than an offset.
    /// </remarks>
    private static DateOnly ExchangeDateOf(IntradayBar bar, TimeZoneInfo exchangeTimeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(bar.Timestamp, exchangeTimeZone).DateTime);
}
