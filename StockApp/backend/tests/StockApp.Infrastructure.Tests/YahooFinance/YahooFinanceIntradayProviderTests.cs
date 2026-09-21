using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using StockApp.Core.MarketData.Exceptions;
using StockApp.Core.MarketData.Models;
using StockApp.Infrastructure.Tests.TestDoubles;
using StockApp.Infrastructure.YahooFinance;

namespace StockApp.Infrastructure.Tests.YahooFinance;

/// <summary>
/// Covers the adapter's two responsibilities: describing the request, and translating whatever
/// comes back — success, failure or silence — into something the domain understands.
/// </summary>
public class YahooFinanceIntradayProviderTests
{
    private const string ValidPayload = """
        {
          "chart": {
            "result": [
              {
                "meta": { "symbol": "TSLA", "exchangeTimezoneName": "America/New_York" },
                "timestamp": [1755610200, 1755611100],
                "indicators": {
                  "quote": [
                    { "low": [100.5, 101.0], "high": [110.25, 111.0], "volume": [1000, 2000] }
                  ]
                }
              }
            ],
            "error": null
          }
        }
        """;

    private static readonly IntradayQuery TslaQuery =
        IntradayQuery.LastMonthOfFifteenMinuteBars(Symbol.Create("TSLA"));

    // ---------------------------------------------------------------- the request

    /// <summary>
    /// The assessment fixes the data being requested: fifteen-minute intervals over one month.
    /// The domain expresses that as enumerations; this is where they become Yahoo's vocabulary.
    /// </summary>
    [Fact]
    public async Task TheRequest_AsksForFifteenMinuteBarsOverOneMonth()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, ValidPayload);

        await GetAsync(handler, TslaQuery);

        Assert.Equal(
            "https://example.test/chart/TSLA?interval=15m&range=1mo",
            handler.LastRequest?.RequestUri?.ToString());
    }

    /// <summary>
    /// Symbols carry characters with meaning in a URL: the ^ of an index, the = of a futures
    /// contract. Escaping them keeps a symbol from altering the request's structure.
    /// </summary>
    /// <remarks>
    /// The expected values record what actually reaches the wire, which is not simply the escaped
    /// string: <see cref="Uri"/> canonicalises the combined address and unescapes characters that
    /// need no escaping in a path segment, so %5E becomes ^ again while %3D is preserved. Both
    /// forms were confirmed against the live API.
    /// </remarks>
    [Theory]
    [InlineData("^GSPC", "https://example.test/chart/^GSPC?interval=15m&range=1mo")]
    [InlineData("ES=F", "https://example.test/chart/ES%3DF?interval=15m&range=1mo")]
    [InlineData("BRK-B", "https://example.test/chart/BRK-B?interval=15m&range=1mo")]
    [InlineData("VOD.L", "https://example.test/chart/VOD.L?interval=15m&range=1mo")]
    public async Task ASymbolWithPunctuation_ReachesTheUrlIntact(string symbol, string expectedUrl)
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, ValidPayload);
        var query = IntradayQuery.LastMonthOfFifteenMinuteBars(Symbol.Create(symbol));

        await GetAsync(handler, query);

        Assert.Equal(expectedUrl, handler.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task ASuccessfulResponse_IsMappedToBars()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, ValidPayload);

        var series = await GetAsync(handler, TslaQuery);

        Assert.Equal(2, series.Bars.Count);
        Assert.Equal(100.5m, series.Bars[0].Low);
        Assert.Equal("America/New_York", series.ExchangeTimeZone.ReportedId);
    }

    [Fact]
    public async Task ANullQuery_IsRejected()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, ValidPayload);

        await Assert.ThrowsAsync<ArgumentNullException>(() => GetAsync(handler, null!));
    }

    [Theory]
    [InlineData((BarInterval)99, Lookback.OneMonth)]
    [InlineData(BarInterval.FifteenMinutes, (Lookback)99)]
    public async Task AnUnsupportedIntervalOrRange_IsRejectedBeforeAnyRequestIsSent(
        BarInterval interval, Lookback lookback)
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, ValidPayload);
        var query = new IntradayQuery(Symbol.Create("TSLA"), interval, lookback);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GetAsync(handler, query));
        Assert.Null(handler.LastRequest);
    }

    // ---------------------------------------------------------------- failure translation

    [Fact]
    public async Task NotFound_BecomesAnUnknownSymbol_CarryingTheUpstreamExplanation()
    {
        const string body = """
            {"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}
            """;
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.NotFound, body);

        var exception = await Assert.ThrowsAsync<SymbolNotFoundException>(() => GetAsync(handler, TslaQuery));

        Assert.Equal("No data found, symbol may be delisted", exception.UpstreamDescription);
    }

    /// <summary>The status code already carries the meaning, so an unreadable body is not worth failing over.</summary>
    [Fact]
    public async Task NotFound_WithAnUnreadableBody_StillBecomesAnUnknownSymbol()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.NotFound, "<html>not json</html>");

        var exception = await Assert.ThrowsAsync<SymbolNotFoundException>(() => GetAsync(handler, TslaQuery));

        Assert.Null(exception.UpstreamDescription);
    }

    [Fact]
    public async Task TooManyRequests_BecomesRateLimited_PreservingRetryAfter()
    {
        var response = StubHttpMessageHandler.Json(HttpStatusCode.TooManyRequests, "{}");
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        var handler = new StubHttpMessageHandler(response);

        var exception = await Assert.ThrowsAsync<UpstreamRateLimitedException>(() => GetAsync(handler, TslaQuery));

        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
    }

    [Fact]
    public async Task TooManyRequests_WithoutRetryAfter_LeavesTheDelayUnknown()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.TooManyRequests, "{}");

        var exception = await Assert.ThrowsAsync<UpstreamRateLimitedException>(() => GetAsync(handler, TslaQuery));

        Assert.Null(exception.RetryAfter);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AnyOtherFailureStatus_BecomesUnavailable(HttpStatusCode statusCode)
    {
        var handler = StubHttpMessageHandler.Returning(statusCode, "{}");

        var exception = await Assert.ThrowsAsync<UpstreamUnavailableException>(() => GetAsync(handler, TslaQuery));

        Assert.Contains(
            ((int)statusCode).ToString(CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATransportFailure_BecomesUnavailable()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("no route to host"));

        var exception = await Assert.ThrowsAsync<UpstreamUnavailableException>(() => GetAsync(handler, TslaQuery));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    /// <summary>
    /// A cancelled task with an uncancelled token means the upstream ran out of time, which is a
    /// server-side failure worth reporting.
    /// </summary>
    [Fact]
    public async Task AnUpstreamTimeout_BecomesUnavailable()
    {
        var handler = StubHttpMessageHandler.Throwing(new TaskCanceledException("timed out"));

        var exception = await Assert.ThrowsAsync<UpstreamUnavailableException>(() => GetAsync(handler, TslaQuery));

        Assert.Contains("did not respond in time", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A caller who walks away is not an upstream failure. The cancellation propagates untouched so
    /// a closed browser tab never surfaces as a server error.
    /// </summary>
    [Fact]
    public async Task ACallerCancelling_IsNotReportedAsAnUpstreamFailure()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, ValidPayload);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GetWithTokenAsync(handler, TslaQuery, cancelled.Token));
    }

    [Theory]
    [InlineData("<html>not json at all</html>")]
    [InlineData("{ \"chart\": { \"result\": [ { \"timestamp\": \"not an array\" } ] } }")]
    public async Task AnUnparseableSuccessBody_BecomesAContractViolation(string body)
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, body);

        await Assert.ThrowsAsync<UpstreamContractException>(() => GetAsync(handler, TslaQuery));
    }

    /// <summary>
    /// An upstream that starts sending an unreadable encoding is another way its contract can change.
    /// It must still surface as a contract violation rather than an unhandled error.
    /// </summary>
    [Fact]
    public async Task AnUnsupportedCharset_BecomesAContractViolation()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidPayload, Encoding.UTF8, "application/json")
        };
        response.Content.Headers.ContentType!.CharSet = "utf-7";
        var handler = new StubHttpMessageHandler(response);

        await Assert.ThrowsAsync<UpstreamContractException>(() => GetAsync(handler, TslaQuery));
    }

    /// <summary>A literal <c>null</c> body parses successfully into nothing, and means no data.</summary>
    [Fact]
    public async Task ANullSuccessBody_IsTreatedAsAnUnknownSymbol()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "null");

        await Assert.ThrowsAsync<SymbolNotFoundException>(() => GetAsync(handler, TslaQuery));
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Runs the provider under the test framework's own cancellation token, so a hung test can be
    /// cancelled rather than blocking the run.
    /// </summary>
    private static Task<IntradaySeries> GetAsync(StubHttpMessageHandler handler, IntradayQuery query) =>
        GetWithTokenAsync(handler, query, TestContext.Current.CancellationToken);

    private static async Task<IntradaySeries> GetWithTokenAsync(
        StubHttpMessageHandler handler,
        IntradayQuery query,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/chart/")
        };

        var provider = new YahooFinanceIntradayProvider(
            httpClient,
            NullLogger<YahooFinanceIntradayProvider>.Instance);

        return await provider.GetIntradayAsync(query, cancellationToken);
    }
}
