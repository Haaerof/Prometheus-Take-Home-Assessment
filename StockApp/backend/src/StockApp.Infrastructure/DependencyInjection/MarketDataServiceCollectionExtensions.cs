using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
            .AddStandardResilienceHandler();

        return services;
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
