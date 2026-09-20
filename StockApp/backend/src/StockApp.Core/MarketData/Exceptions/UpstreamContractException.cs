namespace StockApp.Core.MarketData.Exceptions;

/// <summary>The data source answered in a shape this application cannot interpret.</summary>
public sealed class UpstreamContractException(string message, Exception? innerException = null)
    : MarketDataException(message, innerException);
