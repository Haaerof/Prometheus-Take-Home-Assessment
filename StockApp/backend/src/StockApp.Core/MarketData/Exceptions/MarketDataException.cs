namespace StockApp.Core.MarketData.Exceptions;

/// <summary>
/// Base type for failures that originate with the market data source rather than with this application.
/// </summary>
/// <remarks>
/// A single base type lets the web layer translate every upstream failure to a response in one
/// place, while each subclass still carries enough detail to choose the right status code.
/// </remarks>
public abstract class MarketDataException(string message, Exception? innerException = null)
    : Exception(message, innerException);
