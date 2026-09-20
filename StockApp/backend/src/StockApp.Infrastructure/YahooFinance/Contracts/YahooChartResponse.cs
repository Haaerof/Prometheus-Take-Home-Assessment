using System.Text.Json.Serialization;

namespace StockApp.Infrastructure.YahooFinance.Contracts;

/// <summary>The root of the Yahoo chart payload.</summary>
/// <remarks>
/// The contract types in this folder mirror the wire format exactly and are deliberately
/// permissive: every field is nullable because the payload makes no promises. They are
/// <see langword="internal"/> so the vendor's shape cannot leak past this assembly.
/// </remarks>
internal sealed record YahooChartResponse(
    [property: JsonPropertyName("chart")] YahooChart? Chart);
