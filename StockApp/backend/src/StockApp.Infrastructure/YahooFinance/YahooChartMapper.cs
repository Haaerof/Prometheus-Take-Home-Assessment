using Microsoft.Extensions.Logging;
using StockApp.Core.MarketData.Exceptions;
using StockApp.Core.MarketData.Models;
using StockApp.Infrastructure.YahooFinance.Contracts;

namespace StockApp.Infrastructure.YahooFinance;

/// <summary>
/// Translates a Yahoo chart payload into the domain model.
/// </summary>
/// <remarks>
/// This is where the wire format's permissiveness is traded for the domain model's guarantees:
/// parallel arrays become bars, missing values disappear, and an unusable payload becomes an
/// exception. Every bar that survives this class is complete, so nothing downstream checks for nulls.
/// </remarks>
internal static partial class YahooChartMapper
{
    /// <summary>Longest exchange name accepted; real ones are a few characters.</summary>
    private const int MaxExchangeNameLength = 100;

    /// <summary>Converts <paramref name="response"/> into a series for <paramref name="symbol"/>.</summary>
    /// <exception cref="SymbolNotFoundException">The payload carries no result for the symbol.</exception>
    /// <exception cref="UpstreamContractException">The payload is missing structure we require.</exception>
    public static IntradaySeries ToIntradaySeries(YahooChartResponse? response, Symbol symbol, ILogger logger)
    {
        if (response?.Chart?.Result is not { Count: > 0 } results)
        {
            throw new SymbolNotFoundException(symbol, response?.Chart?.Error?.Description);
        }

        var result = results[0];

        if (result.Indicators?.Quote is not { Count: > 0 } quotes)
        {
            throw new UpstreamContractException($"The response for '{symbol}' contained no quote data.");
        }

        var timeZone = ResolveTimeZone(result.Meta?.ExchangeTimezoneName, symbol, logger);
        var bars = ReadBars(result.Timestamp, quotes[0], symbol, logger);

        return new IntradaySeries(symbol, timeZone, bars, CleanExchangeName(result.Meta?.FullExchangeName));
    }

    /// <summary>
    /// Accepts the reported exchange name only if it is plain, printable text.
    /// </summary>
    /// <remarks>
    /// This value is published in a response header, and a header cannot contain control characters:
    /// a name arriving with a carriage return would otherwise fail the whole request when the
    /// response is written. It is upstream data, so it is treated as untrusted and dropped when it
    /// is not usable, rather than being repaired into something the source never said.
    /// </remarks>
    private static string? CleanExchangeName(string? reported)
    {
        var trimmed = reported?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxExchangeNameLength)
        {
            return null;
        }

        return trimmed.Any(char.IsControl) ? null : trimmed;
    }

    /// <summary>
    /// Resolves the reported IANA identifier, falling back to UTC when this host does not know it.
    /// </summary>
    /// <remarks>
    /// Falling back keeps the data usable, but grouping may then split a session across two days,
    /// so the fallback is recorded on the result for callers to surface rather than hidden here.
    /// </remarks>
    private static ExchangeTimeZone ResolveTimeZone(string? reportedId, Symbol symbol, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(reportedId))
        {
            LogMissingTimeZone(logger, symbol);
            return ExchangeTimeZone.FallbackToUtc("(unspecified)");
        }

        try
        {
            return ExchangeTimeZone.Resolved(reportedId, TimeZoneInfo.FindSystemTimeZoneById(reportedId));
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            LogUnknownTimeZone(logger, exception, reportedId, symbol);
            return ExchangeTimeZone.FallbackToUtc(reportedId);
        }
    }

    /// <summary>
    /// Zips the parallel arrays into bars, discarding any interval that is not fully populated.
    /// </summary>
    /// <remarks>
    /// The arrays are read to the shortest common length. They should always agree, but indexing one
    /// array by another's length would pair a timestamp with the wrong price if they ever did not.
    /// </remarks>
    private static List<IntradayBar> ReadBars(
        IReadOnlyList<long?>? timestamps,
        YahooQuote quote,
        Symbol symbol,
        ILogger logger)
    {
        if (timestamps is null || quote.Low is null || quote.High is null || quote.Volume is null)
        {
            return [];
        }

        var count = Math.Min(
            timestamps.Count,
            Math.Min(quote.Low.Count, Math.Min(quote.High.Count, quote.Volume.Count)));

        if (count < timestamps.Count)
        {
            LogTruncatedQuoteArrays(logger, symbol, count, timestamps.Count);
        }

        var bars = new List<IntradayBar>(count);

        for (var index = 0; index < count; index++)
        {
            if (timestamps[index] is not { } epochSeconds ||
                quote.Low[index] is not { } low ||
                quote.High[index] is not { } high ||
                quote.Volume[index] is not { } volume)
            {
                continue;
            }

            bars.Add(new IntradayBar(DateTimeOffset.FromUnixTimeSeconds(epochSeconds), low, high, volume));
        }

        if (count > bars.Count)
        {
            LogDiscardedIntervals(logger, count - bars.Count, symbol);
        }

        return bars;
    }

    // Source-generated log methods. The generator emits the formatting and the "is this level
    // enabled?" check, so nothing is allocated or evaluated when the level is switched off.
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No exchange time zone reported for {Symbol}; grouping by UTC.")]
    private static partial void LogMissingTimeZone(ILogger logger, Symbol symbol);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Exchange time zone '{TimeZoneId}' reported for {Symbol} is unknown on this host; grouping by UTC.")]
    private static partial void LogUnknownTimeZone(
        ILogger logger, Exception exception, string timeZoneId, Symbol symbol);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Quote arrays for {Symbol} were shorter than the timestamp array ({QuoteCount} of {TimestampCount}).")]
    private static partial void LogTruncatedQuoteArrays(
        ILogger logger, Symbol symbol, int quoteCount, int timestampCount);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Discarded {DiscardedCount} incomplete intervals for {Symbol}.")]
    private static partial void LogDiscardedIntervals(ILogger logger, int discardedCount, Symbol symbol);
}
