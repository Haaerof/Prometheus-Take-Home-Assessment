namespace StockApp.Api.Contracts;

/// <summary>
/// Stable identifiers for the ways a request can fail, published as the <c>errorCode</c> member of
/// every problem response.
/// </summary>
/// <remarks>
/// A status code alone is too coarse for a client to act on, and the <c>title</c> is prose that may
/// be reworded or translated. These codes are part of the published contract: a client can branch on
/// them to choose what to show, and they will not change without a deliberate contract change.
/// </remarks>
public static class ApiErrorCode
{
    /// <summary>The requested symbol is not a well-formed ticker. The caller should correct it.</summary>
    public const string InvalidSymbol = "INVALID_SYMBOL";

    /// <summary>The requested rounding strategy is not one this API supports.</summary>
    public const string InvalidRounding = "INVALID_ROUNDING";

    /// <summary>The symbol is well formed but the data source has no such instrument.</summary>
    public const string SymbolNotFound = "SYMBOL_NOT_FOUND";

    /// <summary>The data source is throttling requests. The caller may retry later.</summary>
    public const string UpstreamRateLimited = "UPSTREAM_RATE_LIMITED";

    /// <summary>The data source could not be reached or failed while answering. Retrying may help.</summary>
    public const string UpstreamUnavailable = "UPSTREAM_UNAVAILABLE";

    /// <summary>
    /// The data source answered in a shape this application cannot interpret, which usually means its
    /// API has changed. Retrying will not help; this needs attention from us.
    /// </summary>
    public const string UpstreamContract = "UPSTREAM_CONTRACT";
}
