using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockApp.Core.MarketData.Abstractions;
using StockApp.Core.MarketData.Exceptions;
using StockApp.Core.MarketData.Models;
using StockApp.Infrastructure.DependencyInjection;
using StockApp.Infrastructure.Tests.TestDoubles;

namespace StockApp.Infrastructure.Tests.DependencyInjection;

/// <summary>
/// Covers how many times a failing data source is actually called.
/// </summary>
/// <remarks>
/// These exercise the registration rather than the provider, because the retry policy lives in the
/// HTTP pipeline the container builds. Counting attempts is the only way to tell a policy that
/// retries from one that does not: both produce the same error.
/// </remarks>
public class ResiliencePolicyTests
{
    /// <summary>
    /// Retrying a source that is already throttling multiplies the requests it is rejecting, so
    /// <c>429</c> is answered immediately rather than attempted again.
    /// </summary>
    [Fact]
    public async Task ARateLimitedSource_IsCalledOnce()
    {
        var (provider, handler) = BuildProvider(HttpStatusCode.TooManyRequests);

        await Assert.ThrowsAsync<UpstreamRateLimitedException>(
            () => provider.GetIntradayAsync(Query, TestContext.Current.CancellationToken));

        Assert.Equal(1, handler.Attempts);
    }

    /// <summary>A server error may well be transient, so it is still worth further attempts.</summary>
    [Fact]
    public async Task AFailingSource_IsRetried()
    {
        var (provider, handler) = BuildProvider(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => provider.GetIntradayAsync(Query, TestContext.Current.CancellationToken));

        Assert.True(handler.Attempts > 1, $"expected more than one attempt, saw {handler.Attempts}");
    }

    private static IntradayQuery Query => IntradayQuery.LastMonthOfFifteenMinuteBars(Symbol.Create("TSLA"));

    /// <summary>
    /// Builds the provider exactly as the application does, with only the innermost handler replaced.
    /// </summary>
    private static (IIntradayDataProvider Provider, CountingHandler Handler) BuildProvider(HttpStatusCode status)
    {
        var handler = new CountingHandler(status);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddYahooFinanceMarketData(new ConfigurationBuilder().Build());

        // Replaces only the innermost handler, so the resilience pipeline the application
        // registers above it is the one under test.
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => handler));

        var serviceProvider = services.BuildServiceProvider();

        return (serviceProvider.GetRequiredService<IIntradayDataProvider>(), handler);
    }

    /// <summary>Answers with a fixed status and records how many attempts reached it.</summary>
    private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.FromResult(StubHttpMessageHandler.Json(status, "{}"));
        }
    }
}
