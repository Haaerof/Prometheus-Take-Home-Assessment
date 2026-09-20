using System.Globalization;
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

    private static async Task<string> GetBodyAsync(
        IReadOnlyDictionary<string, string?>? configuration,
        decimal low = Awkward)
    {
        var series = new IntradaySeries(
            Symbol.Create("TSLA"),
            ExchangeTimeZone.Resolved(NewYork, TimeZoneInfo.FindSystemTimeZoneById(NewYork)),
            [new IntradayBar(DateTimeOffset.Parse("2026-09-14T14:30:00Z", CultureInfo.InvariantCulture), low, 1m, 1L)]);

        using var factory = new StockApiFactory(new StubIntradayDataProvider(series), configuration);
        using var client = factory.CreateClient();

        return await client.GetStringAsync(
            new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative),
            TestContext.Current.CancellationToken);
    }
}
