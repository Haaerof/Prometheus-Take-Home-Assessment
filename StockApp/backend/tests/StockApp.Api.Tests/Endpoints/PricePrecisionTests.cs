using System.Globalization;
using System.Net;
using System.Text.Json;
using StockApp.Api.Contracts;
using StockApp.Api.Tests.TestDoubles;
using StockApp.Core.MarketData.Models;

namespace StockApp.Api.Tests.Endpoints;

/// <summary>
/// Covers the configurable price precision: the default convention, and the settings that change it.
/// </summary>
/// <remarks>
/// The default — four decimal places, halves away from zero — is what the assessment specifies and
/// what EU Regulation 1103/97 and Regulation DD require for published monetary amounts. The other
/// strategies exist so a deployment bound by a different convention can say so in configuration
/// rather than in code.
/// </remarks>
public class PricePrecisionTests
{
    private const string NewYork = "America/New_York";

    /// <summary>An average whose fifth decimal forces every strategy to a different answer.</summary>
    private const decimal Awkward = 345.22059983m;

    private static IntradaySeries SeriesWith(decimal low) =>
        new(Symbol.Create("TSLA"),
            ExchangeTimeZone.Resolved(NewYork, TimeZoneInfo.FindSystemTimeZoneById(NewYork)),
            [new IntradayBar(DateTimeOffset.Parse("2026-09-14T14:30:00Z", CultureInfo.InvariantCulture), low, 1m, 1L)]);

    [Fact]
    public async Task ByDefault_PricesAreRoundedAwayFromZeroToFourPlaces()
    {
        var body = await GetBodyAsync(configuration: null);

        Assert.Contains("\"lowAverage\":345.2206", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Truncation, requested as <c>ToZero</c>. Every value is cut off rather than rounded, which is
    /// why it is available but not the default: the error is always in the same direction.
    /// </summary>
    [Fact]
    public async Task Truncation_CanBeSelectedInConfiguration()
    {
        var body = await GetBodyAsync(new Dictionary<string, string?>
        {
            ["PricePrecision:Rounding"] = nameof(MidpointRounding.ToZero)
        });

        Assert.Contains("\"lowAverage\":345.2205", body, StringComparison.Ordinal);
    }

    /// <summary>Banker's rounding, the .NET default, is equally available to a caller that requires it.</summary>
    [Fact]
    public async Task BankersRounding_CanBeSelectedInConfiguration()
    {
        var body = await GetBodyAsync(
            new Dictionary<string, string?> { ["PricePrecision:Rounding"] = nameof(MidpointRounding.ToEven) },
            low: 1.00005m);

        Assert.Contains("\"lowAverage\":1.0000", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, "345.22")]
    [InlineData(4, "345.2206")]
    [InlineData(6, "345.220600")]
    public async Task TheNumberOfDecimalPlaces_FollowsConfiguration(int places, string expected)
    {
        var body = await GetBodyAsync(new Dictionary<string, string?>
        {
            ["PricePrecision:DecimalPlaces"] = places.ToString(CultureInfo.InvariantCulture)
        });

        Assert.Contains($"\"lowAverage\":{expected}", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// A nonsensical setting stops the application at startup rather than producing quietly wrong
    /// figures once traffic arrives.
    /// </summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("28")]
    public async Task AnImpossiblePrecision_PreventsTheApplicationFromStarting(string places)
    {
        var configuration = new Dictionary<string, string?> { ["PricePrecision:DecimalPlaces"] = places };

        await Assert.ThrowsAnyAsync<Exception>(() => GetBodyAsync(configuration));
    }

    /// <summary>
    /// The UI offers the choice per search, so the strategy travels on the request. It overrides the
    /// configured default rather than replacing it.
    /// </summary>
    [Theory]
    [InlineData("ToZero", "345.2205")]
    [InlineData("tozero", "345.2205")]
    [InlineData("AwayFromZero", "345.2206")]
    [InlineData("ToEven", "345.2206")]
    public async Task ACallerCanChooseTheStrategyPerRequest(string requested, string expected)
    {
        var body = await GetBodyAsync(configuration: null, query: $"?rounding={requested}");

        Assert.Contains($"\"lowAverage\":{expected}", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARequestedStrategy_OverridesTheConfiguredDefault()
    {
        var configuration = new Dictionary<string, string?>
        {
            ["PricePrecision:Rounding"] = nameof(MidpointRounding.ToZero)
        };

        var body = await GetBodyAsync(configuration, query: "?rounding=AwayFromZero");

        Assert.Contains("\"lowAverage\":345.2206", body, StringComparison.Ordinal);
    }

    /// <summary>An unknown strategy is refused by name rather than quietly falling back to a default.</summary>
    [Theory]
    [InlineData("Sideways")]
    [InlineData("3")]
    public async Task AnUnknownStrategy_IsRejected(string requested)
    {
        using var api = CreateApi();

        var response = await api.Client.GetAsync(
            new Uri($"/api/v1/stocks/TSLA/daily?rounding={requested}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var problem = JsonDocument
            .Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .RootElement;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ApiErrorCode.InvalidRounding, problem.GetProperty("errorCode").GetString());
    }

    private static async Task<string> GetBodyAsync(
        IReadOnlyDictionary<string, string?>? configuration,
        decimal low = Awkward,
        string query = "")
    {
        using var factory = new StockApiFactory(new StubIntradayDataProvider(SeriesWith(low)), configuration);
        using var client = factory.CreateClient();

        return await client.GetStringAsync(
            new Uri($"/api/v1/stocks/TSLA/daily{query}", UriKind.Relative),
            TestContext.Current.CancellationToken);
    }

    private static StockApiFactoryScope CreateApi() => new(SeriesWith(Awkward));

    /// <summary>Owns an in-memory host and a client pointed at it for a single test.</summary>
    private sealed class StockApiFactoryScope : IDisposable
    {
        private readonly StockApiFactory _factory;

        public StockApiFactoryScope(IntradaySeries series)
        {
            _factory = new StockApiFactory(new StubIntradayDataProvider(series));
            Client = _factory.CreateClient();
        }

        public HttpClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
        }
    }
}
