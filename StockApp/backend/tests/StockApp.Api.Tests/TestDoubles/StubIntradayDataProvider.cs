using StockApp.Core.MarketData.Abstractions;
using StockApp.Core.MarketData.Models;

namespace StockApp.Api.Tests.TestDoubles;

/// <summary>
/// Stands in for the Yahoo adapter so endpoint tests exercise the web layer without a network call.
/// </summary>
/// <remarks>
/// Written by hand rather than generated: the port has a single method, and a fake that states
/// plainly what it returns doubles as documentation of the contract the endpoint depends on.
/// </remarks>
internal sealed class StubIntradayDataProvider(Func<IntradayQuery, IntradaySeries> respond)
    : IIntradayDataProvider
{
    /// <summary>The query the web layer passed down, for asserting on what the endpoint asked for.</summary>
    public IntradayQuery? LastQuery { get; private set; }

    /// <summary>Always returns <paramref name="series"/>.</summary>
    public StubIntradayDataProvider(IntradaySeries series)
        : this(_ => series)
    {
    }

    /// <summary>Fails every request the way the real adapter reports that kind of failure.</summary>
    public static StubIntradayDataProvider Failing(Exception exception) =>
        new(_ => throw exception);

    /// <inheritdoc />
    public Task<IntradaySeries> GetIntradayAsync(IntradayQuery query, CancellationToken cancellationToken)
    {
        LastQuery = query;
        return Task.FromResult(respond(query));
    }
}
