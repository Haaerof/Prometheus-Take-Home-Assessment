using System.Text.Json.Serialization;

namespace StockApp.Infrastructure.YahooFinance.Contracts;

/// <summary>Either a result or an error, never both.</summary>
internal sealed record YahooChart(
    [property: JsonPropertyName("result")] IReadOnlyList<YahooChartResult>? Result,
    [property: JsonPropertyName("error")] YahooError? Error);
