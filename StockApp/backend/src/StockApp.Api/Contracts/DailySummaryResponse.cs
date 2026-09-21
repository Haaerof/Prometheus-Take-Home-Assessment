using System.Text.Json.Serialization;
using StockApp.Core.MarketData.Models;

namespace StockApp.Api.Contracts;

/// <summary>
/// One day of aggregated trading, in the shape published to clients.
/// </summary>
/// <param name="Day">The exchange-local trading date, as <c>yyyy-MM-dd</c>.</param>
/// <param name="LowAverage">Mean of the day's interval lows, to four decimal places.</param>
/// <param name="HighAverage">Mean of the day's interval highs, to four decimal places.</param>
/// <param name="Volume">Total shares traded during the day.</param>
/// <remarks>
/// Kept separate from <see cref="DailySummary"/> on purpose. The domain model is free to change
/// shape as the application grows; this type is a promise to callers and changes only deliberately.
/// Prices are written at the configured precision by
/// <see cref="Serialization.PriceJsonConverter"/> rather than being reduced here.
/// </remarks>
public sealed record DailySummaryResponse(
    [property: JsonPropertyName("day")] DateOnly Day,
    [property: JsonPropertyName("lowAverage")] decimal LowAverage,
    [property: JsonPropertyName("highAverage")] decimal HighAverage,
    [property: JsonPropertyName("volume")] long Volume)
{
    /// <summary>
    /// Projects a domain summary onto the published contract, reduced to the requested precision.
    /// </summary>
    /// <remarks>
    /// Reducing here rather than only at serialisation is what lets a caller choose a strategy per
    /// request. The serializer formats to the same number of places afterwards, which leaves an
    /// already-reduced value untouched.
    /// </remarks>
    public static DailySummaryResponse From(DailySummary summary, int decimalPlaces, MidpointRounding rounding)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new DailySummaryResponse(
            summary.Day,
            Math.Round(summary.LowAverage, decimalPlaces, rounding),
            Math.Round(summary.HighAverage, decimalPlaces, rounding),
            summary.Volume);
    }
}
