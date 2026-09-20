using StockApp.Core.MarketData.Models;

namespace StockApp.Core.MarketData.Abstractions;

/// <summary>
/// Answers the application's central question: what did each trading day look like for this symbol?
/// </summary>
public interface IDailySummaryService
{
    /// <summary>Fetches intraday data for <paramref name="symbol"/> and reduces it to daily summaries.</summary>
    Task<DailySummaryReport> GetLastMonthAsync(Symbol symbol, CancellationToken cancellationToken);
}
