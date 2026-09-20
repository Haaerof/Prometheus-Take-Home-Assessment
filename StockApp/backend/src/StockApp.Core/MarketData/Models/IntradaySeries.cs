namespace StockApp.Core.MarketData.Models;

/// <summary>
/// Intraday bars for one symbol, paired with the exchange clock they must be interpreted against.
/// </summary>
/// <param name="Symbol">The symbol the bars belong to.</param>
/// <param name="ExchangeTimeZone">The clock that decides which calendar day each bar falls on.</param>
/// <param name="Bars">The bars, in the order the data source returned them.</param>
/// <remarks>
/// Bars and time zone travel together deliberately: a bar's timestamp is an absolute instant, so the
/// question "which day is this?" has no answer until a clock is named. Keeping them in one type makes
/// it impossible to group bars without having decided which clock applies.
/// </remarks>
public sealed record IntradaySeries(
    Symbol Symbol,
    ExchangeTimeZone ExchangeTimeZone,
    IReadOnlyList<IntradayBar> Bars);
