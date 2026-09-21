using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using StockApp.Core.MarketData.Exceptions;
using StockApp.Core.MarketData.Models;
using StockApp.Infrastructure.YahooFinance;
using StockApp.Infrastructure.YahooFinance.Contracts;

namespace StockApp.Infrastructure.Tests.YahooFinance;

/// <summary>
/// Covers the translation from Yahoo's wire format to the domain model, which is where the
/// payload's permissiveness is traded for the domain's guarantees.
/// </summary>
public class YahooChartMapperTests
{
    private static readonly Symbol Tsla = Symbol.Create("TSLA");

    /// <summary>Matches how the web host is configured, so the fixture parses as it would in production.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    // ---------------------------------------------------------------- unusable payloads

    [Fact]
    public void ANullPayload_IsTreatedAsAnUnknownSymbol()
    {
        Assert.Throws<SymbolNotFoundException>(() => Map(null));
    }

    [Fact]
    public void APayloadWithNoChart_IsTreatedAsAnUnknownSymbol()
    {
        Assert.Throws<SymbolNotFoundException>(() => Map(new YahooChartResponse(null)));
    }

    [Fact]
    public void APayloadWithNoResult_IsTreatedAsAnUnknownSymbol()
    {
        Assert.Throws<SymbolNotFoundException>(() => Map(new YahooChartResponse(new YahooChart(null, null))));
    }

    [Fact]
    public void AnEmptyResultList_IsTreatedAsAnUnknownSymbol()
    {
        Assert.Throws<SymbolNotFoundException>(() => Map(new YahooChartResponse(new YahooChart([], null))));
    }

    /// <summary>Yahoo explains itself on failure, and that wording is worth passing on to the caller.</summary>
    [Fact]
    public void TheUpstreamExplanation_IsCarriedOnTheException()
    {
        var payload = new YahooChartResponse(
            new YahooChart(null, new YahooError("Not Found", "No data found, symbol may be delisted")));

        var exception = Assert.Throws<SymbolNotFoundException>(() => Map(payload));

        Assert.Equal("No data found, symbol may be delisted", exception.UpstreamDescription);
        Assert.Equal(Tsla, exception.Symbol);
    }

    [Fact]
    public void AResultWithNoIndicators_IsAContractViolation()
    {
        var payload = Payload(new YahooChartResult(Meta("America/New_York"), [1L], null));

        Assert.Throws<UpstreamContractException>(() => Map(payload));
    }

    [Fact]
    public void AResultWithNoQuote_IsAContractViolation()
    {
        var payload = Payload(new YahooChartResult(Meta("America/New_York"), [1L], new YahooIndicators(null)));

        Assert.Throws<UpstreamContractException>(() => Map(payload));
    }

    [Fact]
    public void AnEmptyQuoteList_IsAContractViolation()
    {
        var payload = Payload(new YahooChartResult(Meta("America/New_York"), [1L], new YahooIndicators([])));

        Assert.Throws<UpstreamContractException>(() => Map(payload));
    }

    // ---------------------------------------------------------------- the happy path

    [Fact]
    public void ParallelArrays_AreZippedIntoBars()
    {
        var payload = Payload(
            timestamps: [1_755_610_200L, 1_755_611_100L],
            quote: new YahooQuote([100.5m, 101m], [110.25m, 111m], [1_000L, 2_000L]));

        var series = Map(payload);

        Assert.Equal(Tsla, series.Symbol);
        Assert.Collection(
            series.Bars,
            first =>
            {
                Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_755_610_200L), first.Timestamp);
                Assert.Equal(100.5m, first.Low);
                Assert.Equal(110.25m, first.High);
                Assert.Equal(1_000L, first.Volume);
            },
            second => Assert.Equal(101m, second.Low));
    }

    // ---------------------------------------------------------------- missing values

    /// <summary>
    /// A null price means no trading occurred in that interval. Such a bar is discarded rather than
    /// defaulted, because a zero would drag the day's average toward nothing.
    /// </summary>
    [Theory]
    [InlineData(null, 10.0, 10L)]   // no low
    [InlineData(10.0, null, 10L)]   // no high
    [InlineData(10.0, 10.0, null)]  // no volume
    public void AnIntervalMissingAnyValue_IsDiscarded(double? low, double? high, long? volume)
    {
        var payload = Payload(
            timestamps: [1L, 2L],
            quote: new YahooQuote(
                [(decimal?)low, 20m],
                [(decimal?)high, 30m],
                [volume, 40L]));

        var series = Map(payload);

        var bar = Assert.Single(series.Bars);
        Assert.Equal(20m, bar.Low);
    }

    [Fact]
    public void AnIntervalMissingItsTimestamp_IsDiscarded()
    {
        var payload = Payload(
            timestamps: [null, 2L],
            quote: new YahooQuote([10m, 20m], [15m, 30m], [100L, 200L]));

        var bar = Assert.Single(Map(payload).Bars);

        Assert.Equal(20m, bar.Low);
    }

    [Fact]
    public void EveryIntervalIncomplete_YieldsNoBars()
    {
        var payload = Payload(
            timestamps: [1L, 2L],
            quote: new YahooQuote([null, null], [null, null], [null, null]));

        Assert.Empty(Map(payload).Bars);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void AMissingQuoteArray_YieldsNoBars(bool noLows, bool noHighs, bool noVolumes)
    {
        var payload = Payload(
            timestamps: [1L],
            quote: new YahooQuote(
                noLows ? null : [10m],
                noHighs ? null : [15m],
                noVolumes ? null : [100L]));

        Assert.Empty(Map(payload).Bars);
    }

    [Fact]
    public void AMissingTimestampArray_YieldsNoBars()
    {
        var payload = Payload(
            timestamps: null,
            quote: new YahooQuote([10m], [15m], [100L]));

        Assert.Empty(Map(payload).Bars);
    }

    /// <summary>
    /// The arrays should always agree in length. If they ever did not, indexing one by another's
    /// count would pair a timestamp with the wrong price, so the shortest is the limit.
    /// </summary>
    [Fact]
    public void ShorterQuoteArrays_LimitHowManyBarsAreRead()
    {
        var payload = Payload(
            timestamps: [1L, 2L, 3L],
            quote: new YahooQuote([10m, 20m], [15m, 25m], [100L, 200L]));

        var series = Map(payload);

        Assert.Equal(2, series.Bars.Count);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1L), series.Bars[0].Timestamp);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(2L), series.Bars[1].Timestamp);
    }

    // ---------------------------------------------------------------- time zone resolution

    [Fact]
    public void AKnownTimeZone_IsResolved()
    {
        var series = Map(Payload(timestamps: [1L], quote: Quote(), timeZoneId: "Pacific/Auckland"));

        Assert.False(series.ExchangeTimeZone.IsFallback);
        Assert.Equal("Pacific/Auckland", series.ExchangeTimeZone.ReportedId);
        Assert.Equal(
            TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"),
            series.ExchangeTimeZone.TimeZone);
    }

    /// <summary>
    /// An unrecognised identifier degrades to UTC rather than failing the request, but records that
    /// it did so, because grouping may then split a session across two days.
    /// </summary>
    [Fact]
    public void AnUnknownTimeZone_FallsBackToUtcAndSaysSo()
    {
        var series = Map(Payload(timestamps: [1L], quote: Quote(), timeZoneId: "Mars/Olympus_Mons"));

        Assert.True(series.ExchangeTimeZone.IsFallback);
        Assert.Equal("Mars/Olympus_Mons", series.ExchangeTimeZone.ReportedId);
        Assert.Equal(TimeZoneInfo.Utc, series.ExchangeTimeZone.TimeZone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnAbsentTimeZone_FallsBackToUtcAndSaysSo(string? timeZoneId)
    {
        var series = Map(Payload(timestamps: [1L], quote: Quote(), timeZoneId: timeZoneId));

        Assert.True(series.ExchangeTimeZone.IsFallback);
        Assert.Equal("(unspecified)", series.ExchangeTimeZone.ReportedId);
        Assert.Equal(TimeZoneInfo.Utc, series.ExchangeTimeZone.TimeZone);
    }

    [Fact]
    public void AResultWithNoMetadata_FallsBackToUtc()
    {
        var payload = Payload(new YahooChartResult(null, [1L], new YahooIndicators([Quote()])));

        Assert.True(Map(payload).ExchangeTimeZone.IsFallback);
    }

    // ---------------------------------------------------------------- against a real payload

    /// <summary>
    /// A captured Air New Zealand response: 113 intervals of which one is incomplete, on an exchange
    /// whose sessions open at 22:00 UTC the previous day. Guards the mapper against a live payload
    /// without requiring the network.
    /// </summary>
    /// <summary>
    /// The exchange's display name is presentation detail, so it is carried through untouched when
    /// present and simply absent when not — never a reason to reject an otherwise usable payload.
    /// </summary>
    [Fact]
    public void TheExchangeName_IsCarriedThroughWhenReported()
    {
        var payload = Payload(new YahooChartResult(
            Meta("America/New_York", "NasdaqGS"), [1L], new YahooIndicators([Quote()])));

        Assert.Equal("NasdaqGS", Map(payload).ExchangeName);
    }

    [Fact]
    public void AMissingExchangeName_IsNotAnError()
    {
        var payload = Payload(timestamps: [1L], quote: Quote());

        Assert.Null(Map(payload).ExchangeName);
    }

    [Fact]
    public void ARealPayload_MapsToCompleteBarsOnly()
    {
        var payload = JsonSerializer.Deserialize<YahooChartResponse>(
            File.ReadAllText(Path.Combine("Fixtures", "air-nz-5d.json")),
            SerializerOptions);

        var series = YahooChartMapper.ToIntradaySeries(payload, Symbol.Create("AIR.NZ"), NullLogger.Instance);

        Assert.Equal(112, series.Bars.Count);
        Assert.Equal("Pacific/Auckland", series.ExchangeTimeZone.ReportedId);
        Assert.Equal("NZSE", series.ExchangeName);
        Assert.False(series.ExchangeTimeZone.IsFallback);
        Assert.All(series.Bars, bar => Assert.True(bar.High >= bar.Low));
    }

    // ---------------------------------------------------------------- helpers

    private static IntradaySeries Map(YahooChartResponse? payload) =>
        YahooChartMapper.ToIntradaySeries(payload, Tsla, NullLogger.Instance);

    private static YahooQuote Quote() => new([10m], [15m], [100L]);

    private static YahooMeta Meta(string? timeZoneId, string? exchangeName = null) =>
        new("TSLA", timeZoneId, exchangeName);

    private static YahooChartResponse Payload(YahooChartResult result) =>
        new(new YahooChart([result], null));

    private static YahooChartResponse Payload(
        IReadOnlyList<long?>? timestamps,
        YahooQuote quote,
        string? timeZoneId = "America/New_York") =>
        Payload(new YahooChartResult(Meta(timeZoneId), timestamps, new YahooIndicators([quote])));
}
