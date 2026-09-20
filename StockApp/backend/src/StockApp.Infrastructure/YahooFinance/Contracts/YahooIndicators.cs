using System.Text.Json.Serialization;

namespace StockApp.Infrastructure.YahooFinance.Contracts;

/// <summary>Container for the quote arrays. In practice it holds exactly one entry.</summary>
internal sealed record YahooIndicators(
    [property: JsonPropertyName("quote")] IReadOnlyList<YahooQuote>? Quote);
