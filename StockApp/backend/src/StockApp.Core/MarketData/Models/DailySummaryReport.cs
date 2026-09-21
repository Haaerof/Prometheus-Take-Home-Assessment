namespace StockApp.Core.MarketData.Models;

/// <summary>
/// One symbol's intraday data reduced to daily summaries, with the clock used to group them.
/// </summary>
/// <param name="ExchangeTimeZone">The clock the days were measured on, including whether it was a fallback.</param>
/// <param name="Days">One summary per trading day, oldest first.</param>
/// <param name="ExchangeName">The exchange's display name, when the data source reports one.</param>
public sealed record DailySummaryReport(
    ExchangeTimeZone ExchangeTimeZone,
    IReadOnlyList<DailySummary> Days,
    string? ExchangeName = null);
