using System.Text.Json.Serialization;
using StockApp.Api.Serialization;
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
/// </remarks>
public sealed record DailySummaryResponse(
    [property: JsonPropertyName("day")] DateOnly Day,
    [property: JsonPropertyName("lowAverage")]
    [property: JsonConverter(typeof(FourDecimalPlacesConverter))]
    decimal LowAverage,
    [property: JsonPropertyName("highAverage")]
    [property: JsonConverter(typeof(FourDecimalPlacesConverter))]
    decimal HighAverage,
    [property: JsonPropertyName("volume")] long Volume)
{
    /// <summary>Projects a domain summary onto the published contract.</summary>
    public static DailySummaryResponse From(DailySummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new DailySummaryResponse(summary.Day, summary.LowAverage, summary.HighAverage, summary.Volume);
    }
}
