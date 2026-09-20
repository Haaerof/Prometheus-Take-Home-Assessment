using StockApp.Core.MarketData.Models;

namespace StockApp.Core.MarketData.Exceptions;

/// <summary>The data source does not recognise the requested symbol.</summary>
public sealed class SymbolNotFoundException(Symbol symbol, string? upstreamDescription = null)
    : MarketDataException($"No market data found for symbol '{symbol}'.")
{
    /// <summary>The symbol that was not found.</summary>
    public Symbol Symbol { get; } = symbol;

    /// <summary>The explanation the data source gave, when it supplied one.</summary>
    public string? UpstreamDescription { get; } = upstreamDescription;
}
