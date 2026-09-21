using System.Text.Json.Serialization;

namespace StockApp.Infrastructure.YahooFinance.Contracts;

/// <summary>Metadata about the instrument and the exchange it trades on.</summary>
internal sealed record YahooMeta(
    [property: JsonPropertyName("symbol")] string? Symbol,
    [property: JsonPropertyName("exchangeTimezoneName")] string? ExchangeTimezoneName,
    [property: JsonPropertyName("fullExchangeName")] string? FullExchangeName);
