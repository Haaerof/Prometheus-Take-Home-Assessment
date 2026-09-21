using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StockApp.Core.MarketData.Abstractions;
using StockApp.Core.MarketData.Exceptions;
using StockApp.Core.MarketData.Models;
using StockApp.Infrastructure.YahooFinance.Contracts;

namespace StockApp.Infrastructure.YahooFinance;

/// <summary>
/// Fetches intraday bars from the Yahoo Finance chart API.
/// </summary>
/// <remarks>
/// The <see cref="HttpClient"/> is supplied already configured — base address, user agent, timeouts
/// and retry policy are all set where the client is registered, so this class only has to describe
/// one request and translate the answer.
/// </remarks>
internal sealed class YahooFinanceIntradayProvider(
    HttpClient httpClient,
    ILogger<YahooFinanceIntradayProvider> logger) : IIntradayDataProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<IntradaySeries> GetIntradayAsync(IntradayQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requestUri = BuildRequestUri(query);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new UpstreamUnavailableException("The market data source could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A cancelled task with an uncancelled token means the request timed out, not that the
            // caller went away. Distinguishing the two keeps a client disconnect out of the error logs.
            throw new UpstreamUnavailableException("The market data source did not respond in time.", exception);
        }

        using (response)
        {
            await EnsureSuccessAsync(response, query.Symbol, cancellationToken).ConfigureAwait(false);

            YahooChartResponse? payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<YahooChartResponse>(SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                // JsonException covers a body that is not the JSON we expect; NotSupportedException
                // covers one we cannot even decode, such as an unreadable character set. Both mean
                // the source is no longer speaking the contract we were built against.
                throw new UpstreamContractException(
                    $"The market data source returned a response for '{query.Symbol}' that could not be parsed.",
                    exception);
            }

            return YahooChartMapper.ToIntradaySeries(payload, query.Symbol, logger);
        }
    }

    /// <summary>
    /// Builds the relative request URI, translating domain concepts into Yahoo's query vocabulary.
    /// </summary>
    private static string BuildRequestUri(IntradayQuery query) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Uri.EscapeDataString(query.Symbol.Value)}?interval={ToYahooInterval(query.Interval)}&range={ToYahooRange(query.Lookback)}");

    private static string ToYahooInterval(BarInterval interval) => interval switch
    {
        BarInterval.FifteenMinutes => "15m",
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, "Unsupported bar interval.")
    };

    private static string ToYahooRange(Lookback lookback) => lookback switch
    {
        Lookback.OneMonth => "1mo",
        _ => throw new ArgumentOutOfRangeException(nameof(lookback), lookback, "Unsupported lookback period.")
    };

    /// <summary>
    /// Turns an unsuccessful HTTP response into the domain exception that describes it.
    /// </summary>
    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        Symbol symbol,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                // Yahoo explains itself in the body even on a 404, so pass its wording along.
                var description = await ReadErrorDescriptionAsync(response, cancellationToken).ConfigureAwait(false);
                throw new SymbolNotFoundException(symbol, description);

            case HttpStatusCode.TooManyRequests:
                throw new UpstreamRateLimitedException(response.Headers.RetryAfter?.Delta);

            default:
                throw new UpstreamUnavailableException(
                    $"The market data source returned {(int)response.StatusCode} {response.StatusCode}.");
        }
    }

    private static async Task<string?> ReadErrorDescriptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content
                .ReadFromJsonAsync<YahooChartResponse>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return payload?.Chart?.Error?.Description;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // An unreadable error body is not worth failing over; the status code already told us enough.
            return null;
        }
    }
}
