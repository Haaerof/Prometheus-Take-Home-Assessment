using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using StockApp.Core.MarketData.Abstractions;
using StockApp.Core.MarketData.Services;
using StockApp.Infrastructure.YahooFinance;

namespace StockApp.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the market data implementations with the application's service container.
/// </summary>
/// <remarks>
/// Keeping registration here means the web layer asks for "market data backed by Yahoo" in one line
/// and never names the concrete types. Swapping data source is a change to this file alone.
/// </remarks>
public static class MarketDataServiceCollectionExtensions
{
    /// <summary>Registers the domain services and the Yahoo Finance data provider.</summary>
    public static IServiceCollection AddYahooFinanceMarketData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<YahooFinanceOptions>()
            .Bind(configuration.GetSection(YahooFinanceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IDailySummaryCalculator, DailySummaryCalculator>();
        services.AddScoped<IDailySummaryService, DailySummaryService>();

        services
            .AddHttpClient<IIntradayDataProvider, YahooFinanceIntradayProvider>(ConfigureClient)
            // Retry with backoff, a circuit breaker and per-attempt timeouts. The handler owns all
            // timing, which is why the client's own timeout is left effectively unbounded below.
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.Retry.ShouldHandle = arguments => ValueTask.FromResult(IsWorthRetrying(arguments.Outcome));
            });

        return services;
    }

    /// <summary>
    /// Decides which failures are worth another attempt.
    /// </summary>
    /// <remarks>
    /// Deliberately narrower than the default, which treats <c>429 Too Many Requests</c> as transient.
    /// Retrying a source that is already throttling us multiplies the requests it is rejecting —
    /// measured at four calls per browser request — and delays the answer by several seconds to
    /// arrive at the same refusal. Server errors and connection failures are genuinely worth
    /// retrying, so they still are.
    /// </remarks>
    private static bool IsWorthRetrying(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
        {
            return outcome.Exception is HttpRequestException or TimeoutRejectedException;
        }

        return outcome.Result is { } response
            && response.StatusCode is HttpStatusCode.RequestTimeout or >= HttpStatusCode.InternalServerError;
    }

    private static void ConfigureClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<YahooFinanceOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl);

        // Yahoo answers requests without a user agent with 429, so this header is load bearing.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

        // HttpClient.Timeout would cancel across every retry attempt at once, cutting the resilience
        // pipeline short. Per-attempt timeouts are configured on that pipeline instead.
        client.Timeout = Timeout.InfiniteTimeSpan;
    }
}
