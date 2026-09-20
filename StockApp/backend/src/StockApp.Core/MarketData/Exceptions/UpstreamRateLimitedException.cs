namespace StockApp.Core.MarketData.Exceptions;

/// <summary>The data source is throttling requests.</summary>
public sealed class UpstreamRateLimitedException(TimeSpan? retryAfter = null)
    : MarketDataException("The market data source is rate limiting requests.")
{
    /// <summary>How long the source asked us to wait, when it said.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
