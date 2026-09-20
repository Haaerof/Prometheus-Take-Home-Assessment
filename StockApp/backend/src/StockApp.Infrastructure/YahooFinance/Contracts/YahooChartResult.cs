using System.Text.Json.Serialization;

namespace StockApp.Infrastructure.YahooFinance.Contracts;

/// <summary>One symbol's chart data: metadata, timestamps, and the quote arrays.</summary>
internal sealed record YahooChartResult(
    [property: JsonPropertyName("meta")] YahooMeta? Meta,
    [property: JsonPropertyName("timestamp")] IReadOnlyList<long?>? Timestamp,
    [property: JsonPropertyName("indicators")] YahooIndicators? Indicators);
