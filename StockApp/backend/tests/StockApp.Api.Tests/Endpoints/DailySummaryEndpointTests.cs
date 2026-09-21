using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockApp.Api.Contracts;
using StockApp.Api.Tests.TestDoubles;
using StockApp.Core.MarketData.Exceptions;
using StockApp.Core.MarketData.Models;

namespace StockApp.Api.Tests.Endpoints;

/// <summary>
/// Covers the published contract of <c>GET /api/v1/stocks/{symbol}/daily</c>: the exact JSON the
/// assessment specifies, the headers that describe how days were grouped, and the shape of every
/// failure a caller can encounter.
/// </summary>
public class DailySummaryEndpointTests
{
    private const string NewYork = "America/New_York";

    // ---------------------------------------------------------------- the success contract

    /// <summary>
    /// The assessment fixes the response body exactly. This compares the raw bytes rather than a
    /// deserialised object, so a change to property naming, ordering or number formatting fails here.
    /// </summary>
    [Fact]
    public async Task TheResponseBody_MatchesTheSpecifiedFormatExactly()
    {
        using var api = CreateApi(Series(
            Bar("2026-09-14T14:30:00Z", low: 100.5m, high: 110.25m, volume: 1_000),
            Bar("2026-09-14T14:45:00Z", low: 101.5m, high: 111.75m, volume: 2_000)));

        var response = await api.Client.GetAsync(new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative), Token);
        var body = await response.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """[{"day":"2026-09-14","lowAverage":101.0000,"highAverage":111.0000,"volume":3000}]""",
            body);
    }

    /// <summary>
    /// Trailing zeros are part of the specified precision. They survive only because the converter
    /// formats to a fixed width — rounding alone reduces a decimal's scale and would emit 101.
    /// </summary>
    [Theory]
    [InlineData(101, "101.0000")]
    [InlineData(40.29, "40.2900")]
    [InlineData(40.2958, "40.2958")]
    public async Task PricesAreWrittenWithFourDecimalPlaces(decimal low, string expected)
    {
        using var api = CreateApi(Series(Bar("2026-09-14T14:30:00Z", low, high: 1m, volume: 1)));

        var body = await GetStringAsync(api.Client, "TSLA");

        Assert.Contains($"\"lowAverage\":{expected}", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// .NET rounds to even by default, which would turn 1.00005 into 1.0000. The converter asks for
    /// away-from-zero instead, matching what "four decimal places" conventionally means for prices.
    /// </summary>
    [Fact]
    public async Task PricesAtTheMidpoint_RoundAwayFromZero()
    {
        using var api = CreateApi(Series(Bar("2026-09-14T14:30:00Z", low: 1.00005m, high: 2.00015m, volume: 1)));

        var body = await GetStringAsync(api.Client, "TSLA");

        Assert.Contains("\"lowAverage\":1.0001", body, StringComparison.Ordinal);
        Assert.Contains("\"highAverage\":2.0002", body, StringComparison.Ordinal);
    }

    /// <summary>Volume is a count, not a price, so it is written as a whole number and never rounded.</summary>
    [Fact]
    public async Task VolumeIsWrittenAsAWholeNumber_EvenBeyondThirtyTwoBits()
    {
        using var api = CreateApi(Series(
            Bar("2026-09-14T14:30:00Z", low: 1m, high: 1m, volume: 3_000_000_000L)));

        var body = await GetStringAsync(api.Client, "TSLA");

        Assert.Contains("\"volume\":3000000000", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DaysAreReturnedOldestFirst()
    {
        using var api = CreateApi(Series(
            Bar("2026-09-16T14:30:00Z", low: 3m, high: 3m, volume: 1),
            Bar("2026-09-14T14:30:00Z", low: 1m, high: 1m, volume: 1),
            Bar("2026-09-15T14:30:00Z", low: 2m, high: 2m, volume: 1)));

        var days = await GetDaysAsync(api.Client, "TSLA");

        Assert.Equal(["2026-09-14", "2026-09-15", "2026-09-16"], days.Select(day => day.Day));
    }

    /// <summary>
    /// A symbol that exists but has no recent trading is not an error: the answer is "nothing
    /// happened", which is an empty list rather than a 404.
    /// </summary>
    [Fact]
    public async Task ASymbolWithNoTrading_ReturnsAnEmptyList()
    {
        using var api = CreateApi(Series());

        var response = await api.Client.GetAsync(new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(Token));
    }

    /// <summary>Symbols are normalised once, at the edge, so the adapter receives a canonical value.</summary>
    [Fact]
    public async Task TheSymbolReachesTheDataSourceNormalised()
    {
        using var api = CreateApi(Series());

        await api.Client.GetAsync(new Uri("/api/v1/stocks/tsla/daily", UriKind.Relative), Token);

        Assert.Equal("TSLA", api.Provider.LastQuery?.Symbol.Value);
        Assert.Equal(BarInterval.FifteenMinutes, api.Provider.LastQuery?.Interval);
        Assert.Equal(Lookback.OneMonth, api.Provider.LastQuery?.Lookback);
    }

    // ---------------------------------------------------------------- how grouping is reported

    /// <summary>
    /// The body's shape is fixed by the specification, so the clock used to group days is reported
    /// in headers instead. A client needs it to label the dates it displays.
    /// </summary>
    [Fact]
    public async Task TheGroupingTimeZone_IsReportedInAHeader()
    {
        using var api = CreateApi(Series(Bar("2026-09-14T14:30:00Z", 1m, 1m, 1)));

        var response = await api.Client.GetAsync(new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative), Token);

        Assert.Equal(NewYork, Header(response, "X-Grouping-Timezone"));
        Assert.Equal("false", Header(response, "X-Grouping-Timezone-Fallback"));
    }

    /// <summary>
    /// When the exchange's zone cannot be resolved the data is still returned, but grouped in UTC,
    /// which may split a session across two days. The client is told so it can say so.
    /// </summary>
    [Fact]
    public async Task AUtcFallback_IsAnnouncedInAHeader()
    {
        var series = new IntradaySeries(
            Symbol.Create("TSLA"),
            ExchangeTimeZone.FallbackToUtc("Mars/Olympus_Mons"),
            [Bar("2026-09-14T14:30:00Z", 1m, 1m, 1)]);

        using var api = CreateApi(series);

        var response = await api.Client.GetAsync(new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative), Token);

        Assert.Equal("Mars/Olympus_Mons", Header(response, "X-Grouping-Timezone"));
        Assert.Equal("true", Header(response, "X-Grouping-Timezone-Fallback"));
    }

    // ---------------------------------------------------------------- failures

    /// <summary>A malformed symbol is rejected before any request is made to the data source.</summary>
    [Theory]
    [InlineData("TS%20LA")]
    [InlineData("TSLA!")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    public async Task AMalformedSymbol_IsRejectedWithoutCallingTheDataSource(string symbol)
    {
        using var api = CreateApi(Series());

        var response = await api.Client.GetAsync(new Uri($"/api/v1/stocks/{symbol}/daily", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ApiErrorCode.InvalidSymbol, await ErrorCodeAsync(response));
        Assert.Null(api.Provider.LastQuery);
    }

    /// <summary>The data source explains why it has no such symbol, and that wording reaches the caller.</summary>
    [Fact]
    public async Task AnUnknownSymbol_IsReportedAsNotFound_WithTheUpstreamExplanation()
    {
        using var api = CreateApi(StubIntradayDataProvider.Failing(
            new SymbolNotFoundException(Symbol.Create("TSLA"), "No data found, symbol may be delisted")));

        var response = await api.Client.GetAsync(new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative), Token);
        var problem = await ProblemAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ApiErrorCode.SymbolNotFound, problem.GetProperty("errorCode").GetString());
        Assert.Equal("No data found, symbol may be delisted", problem.GetProperty("detail").GetString());
    }

    /// <summary>
    /// Throttling upstream is reported as this service being temporarily unable to answer, with the
    /// upstream's own wait time passed on so a client need not guess.
    /// </summary>
    [Fact]
    public async Task RateLimiting_IsReportedAsUnavailable_WithRetryAfter()
    {
        using var api = CreateApi(StubIntradayDataProvider.Failing(
            new UpstreamRateLimitedException(TimeSpan.FromSeconds(30))));

        var response = await api.Client.GetAsync(new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(ApiErrorCode.UpstreamRateLimited, await ErrorCodeAsync(response));
        Assert.Equal(TimeSpan.FromSeconds(30), response.Headers.RetryAfter?.Delta);
    }

    /// <summary>
    /// The two 502s are deliberately distinguishable. Being briefly unreachable is worth retrying;
    /// the source having changed shape is not, and needs our attention instead.
    /// </summary>
    [Fact]
    public async Task AnUnreachableDataSource_IsReportedAsATransientGatewayFailure()
    {
        using var api = CreateApi(StubIntradayDataProvider.Failing(
            new UpstreamUnavailableException("The market data source could not be reached.")));

        var response = await api.Client.GetAsync(new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(ApiErrorCode.UpstreamUnavailable, await ErrorCodeAsync(response));
    }

    /// <summary>
    /// If the data source changes its API, the service degrades to a clear answer rather than an
    /// unhandled error, and says which kind of failure it was.
    /// </summary>
    [Fact]
    public async Task AChangedUpstreamApi_IsReportedDistinctlyFromAnOutage()
    {
        using var api = CreateApi(StubIntradayDataProvider.Failing(
            new UpstreamContractException("unparseable")));

        var response = await api.Client.GetAsync(new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative), Token);
        var problem = await ProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(ApiErrorCode.UpstreamContract, problem.GetProperty("errorCode").GetString());

        // The upstream's internal wording is not repeated to callers.
        Assert.DoesNotContain("unparseable", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every failure, whichever layer raised it, is a problem document with a stable code. A client
    /// therefore needs one error path, not one per status.
    /// </summary>
    [Theory]
    [InlineData("TS%20LA", null)]
    [InlineData("TSLA", "not-found")]
    [InlineData("TSLA", "rate-limited")]
    [InlineData("TSLA", "unavailable")]
    [InlineData("TSLA", "contract")]
    public async Task EveryFailure_IsAProblemDocumentCarryingAnErrorCode(string symbol, string? failure)
    {
        using var api = failure is null
            ? CreateApi(Series())
            : CreateApi(StubIntradayDataProvider.Failing(FailureFor(failure)));

        var response = await api.Client.GetAsync(new Uri($"/api/v1/stocks/{symbol}/daily", UriKind.Relative), Token);
        var problem = await ProblemAsync(response);

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("errorCode").GetString()));
        Assert.True(problem.TryGetProperty("title", out _));
        Assert.True(problem.TryGetProperty("status", out _));
    }

    // ---------------------------------------------------------------- helpers

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Exception FailureFor(string failure) => failure switch
    {
        "not-found" => new SymbolNotFoundException(Symbol.Create("TSLA")),
        "rate-limited" => new UpstreamRateLimitedException(),
        "unavailable" => new UpstreamUnavailableException("unreachable"),
        _ => new UpstreamContractException("unparseable")
    };

    private static TestApi CreateApi(IntradaySeries series) =>
        CreateApi(new StubIntradayDataProvider(series));

    private static TestApi CreateApi(StubIntradayDataProvider provider) => new(provider);

    /// <summary>
    /// Owns an in-memory host and a client pointed at it, so a test disposes one thing rather than two.
    /// </summary>
    private sealed class TestApi : IDisposable
    {
        private readonly StockApiFactory _factory;

        public TestApi(StubIntradayDataProvider provider)
        {
            Provider = provider;
            _factory = new StockApiFactory(provider);
            Client = _factory.CreateClient();
        }

        public StubIntradayDataProvider Provider { get; }

        public HttpClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
        }
    }

    private static async Task<string> GetStringAsync(HttpClient client, string symbol) =>
        await client.GetStringAsync(new Uri($"/api/v1/stocks/{symbol}/daily", UriKind.Relative), Token);

    private static async Task<IReadOnlyList<DayJson>> GetDaysAsync(HttpClient client, string symbol) =>
        await client.GetFromJsonAsync<List<DayJson>>(
            new Uri($"/api/v1/stocks/{symbol}/daily", UriKind.Relative), Token) ?? [];

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await ProblemAsync(response)).GetProperty("errorCode").GetString();

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static IntradaySeries Series(params IntradayBar[] bars) =>
        new(Symbol.Create("TSLA"),
            ExchangeTimeZone.Resolved(NewYork, TimeZoneInfo.FindSystemTimeZoneById(NewYork)),
            bars);

    private static IntradayBar Bar(string utcTimestamp, decimal low, decimal high, long volume) =>
        new(DateTimeOffset.Parse(utcTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            low, high, volume);

    private sealed record DayJson(string Day, decimal LowAverage, decimal HighAverage, long Volume);
}
