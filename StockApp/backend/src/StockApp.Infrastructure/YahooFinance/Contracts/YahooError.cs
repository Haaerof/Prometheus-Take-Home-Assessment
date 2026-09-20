using System.Text.Json.Serialization;

namespace StockApp.Infrastructure.YahooFinance.Contracts;

/// <summary>The explanation supplied with an unsuccessful response.</summary>
internal sealed record YahooError(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("description")] string? Description);
