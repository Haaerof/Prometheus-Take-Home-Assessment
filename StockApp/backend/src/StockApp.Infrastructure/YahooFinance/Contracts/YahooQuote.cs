using System.Text.Json.Serialization;

namespace StockApp.Infrastructure.YahooFinance.Contracts;

/// <summary>
/// Prices and volumes as parallel arrays: index <c>i</c> of each corresponds to index <c>i</c>
/// of the timestamp array. Individual entries are null when no trading occurred in that interval.
/// </summary>
internal sealed record YahooQuote(
    [property: JsonPropertyName("low")] IReadOnlyList<decimal?>? Low,
    [property: JsonPropertyName("high")] IReadOnlyList<decimal?>? High,
    [property: JsonPropertyName("volume")] IReadOnlyList<long?>? Volume);
