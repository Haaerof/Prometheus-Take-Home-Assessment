namespace StockApp.Core.MarketData.Models;

/// <summary>
/// A request for intraday data: what to fetch, at what resolution, over what period.
/// </summary>
/// <param name="Symbol">The symbol to fetch.</param>
/// <param name="Interval">The width of each bar.</param>
/// <param name="Lookback">How far back to reach.</param>
/// <remarks>
/// Interval and lookback are expressed as domain concepts, not as the data source's query
/// strings. Supporting a user-selectable range later means extending these enumerations and the
/// translation table in the adapter, without touching the abstraction or anything above it.
/// </remarks>
public sealed record IntradayQuery(Symbol Symbol, BarInterval Interval, Lookback Lookback)
{
    /// <summary>The query this application currently exposes: a month of fifteen-minute bars.</summary>
    public static IntradayQuery LastMonthOfFifteenMinuteBars(Symbol symbol) =>
        new(symbol, BarInterval.FifteenMinutes, Lookback.OneMonth);
}
