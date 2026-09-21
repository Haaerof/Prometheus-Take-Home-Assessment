using StockApp.Core.MarketData.Abstractions;
using StockApp.Core.MarketData.Models;

namespace StockApp.Core.MarketData.Services;

/// <inheritdoc cref="IDailySummaryService" />
/// <remarks>
/// This type does no work of its own: it fetches through the port and delegates the arithmetic to the
/// calculator. Both are injected, so either can be replaced — a different data source, or a different
/// definition of a daily summary — without this class changing.
/// </remarks>
public sealed class DailySummaryService(
    IIntradayDataProvider provider,
    IDailySummaryCalculator calculator) : IDailySummaryService
{
    /// <inheritdoc />
    public async Task<DailySummaryReport> GetLastMonthAsync(Symbol symbol, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var query = IntradayQuery.LastMonthOfFifteenMinuteBars(symbol);
        var series = await provider.GetIntradayAsync(query, cancellationToken).ConfigureAwait(false);

        return new DailySummaryReport(
            series.ExchangeTimeZone,
            calculator.Summarise(series),
            series.ExchangeName);
    }
}
