using StockApp.Core.MarketData.Exceptions;
using StockApp.Core.MarketData.Models;

namespace StockApp.Core.MarketData.Abstractions;

/// <summary>
/// Supplies intraday bars from whichever market data source the application is configured against.
/// </summary>
/// <remarks>
/// This is the boundary between the domain and the outside world. Implementations live in the
/// infrastructure layer; nothing here knows about HTTP, JSON or any particular vendor.
/// </remarks>
public interface IIntradayDataProvider
{
    /// <summary>Fetches the bars matching <paramref name="query"/>.</summary>
    /// <exception cref="SymbolNotFoundException">The source does not recognise the symbol.</exception>
    /// <exception cref="UpstreamRateLimitedException">The source is throttling requests.</exception>
    /// <exception cref="UpstreamUnavailableException">The source could not be reached.</exception>
    /// <exception cref="UpstreamContractException">The source replied in an unexpected shape.</exception>
    Task<IntradaySeries> GetIntradayAsync(IntradayQuery query, CancellationToken cancellationToken);
}
