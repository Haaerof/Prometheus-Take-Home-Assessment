namespace StockApp.Core.MarketData.Exceptions;

/// <summary>The data source could not be reached, or failed while answering.</summary>
public sealed class UpstreamUnavailableException(string message, Exception? innerException = null)
    : MarketDataException(message, innerException);
