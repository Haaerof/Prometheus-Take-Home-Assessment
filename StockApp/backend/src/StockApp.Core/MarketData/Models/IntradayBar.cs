namespace StockApp.Core.MarketData.Models;

/// <summary>
/// One intraday interval of trading, such as a single 15-minute window.
/// </summary>
/// <param name="Timestamp">The instant the interval began, as an absolute point in time.</param>
/// <param name="Low">The lowest traded price during the interval.</param>
/// <param name="High">The highest traded price during the interval.</param>
/// <param name="Volume">Shares traded during the interval.</param>
/// <remarks>
/// Every property is non-nullable by design. Intervals with missing prices are discarded where
/// the upstream response is translated, so code downstream never has to ask whether a bar is complete.
/// </remarks>
public sealed record IntradayBar(
    DateTimeOffset Timestamp,
    decimal Low,
    decimal High,
    long Volume);
