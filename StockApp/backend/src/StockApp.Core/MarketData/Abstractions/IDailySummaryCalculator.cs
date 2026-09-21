using StockApp.Core.MarketData.Models;

namespace StockApp.Core.MarketData.Abstractions;

/// <summary>
/// Reduces intraday bars to one summary per trading day.
/// </summary>
public interface IDailySummaryCalculator
{
    /// <summary>
    /// Groups <paramref name="series"/> by exchange-local date and averages each day's prices.
    /// </summary>
    /// <returns>One summary per day that has at least one bar, ordered oldest first.</returns>
    IReadOnlyList<DailySummary> Summarise(IntradaySeries series);
}
