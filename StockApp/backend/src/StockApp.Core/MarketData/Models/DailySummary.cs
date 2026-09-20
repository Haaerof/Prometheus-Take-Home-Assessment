namespace StockApp.Core.MarketData.Models;

/// <summary>
/// One trading day's aggregate, measured on the exchange's own calendar.
/// </summary>
/// <param name="Day">The exchange-local date.</param>
/// <param name="LowAverage">The mean of the day's interval lows, at full precision.</param>
/// <param name="HighAverage">The mean of the day's interval highs, at full precision.</param>
/// <param name="Volume">Total shares traded across the day's intervals.</param>
/// <remarks>
/// Averages are deliberately not rounded here. How many decimal places a consumer sees is a property
/// of the published contract, not of the calculation, so rounding happens where the response is written.
/// </remarks>
public sealed record DailySummary(
    DateOnly Day,
    decimal LowAverage,
    decimal HighAverage,
    long Volume);
