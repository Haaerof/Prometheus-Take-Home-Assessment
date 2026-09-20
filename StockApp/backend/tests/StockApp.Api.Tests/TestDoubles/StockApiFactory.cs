using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockApp.Core.MarketData.Abstractions;

namespace StockApp.Api.Tests.TestDoubles;

/// <summary>
/// Boots the real application in memory with the market data source replaced.
/// </summary>
/// <remarks>
/// Everything else — routing, model binding, serialisation, the exception handler, the JSON
/// converters — is exactly what runs in production, so these tests exercise the published contract
/// rather than a reconstruction of it. This is the equivalent of Spring's @SpringBootTest with the
/// data source swapped for a stub.
/// </remarks>
internal sealed class StockApiFactory(
    IIntradayDataProvider provider,
    IReadOnlyDictionary<string, string?>? configuration = null) : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Settings supplied by a test override appsettings.json, exactly as an environment
        // variable would in a deployment.
        if (configuration is not null)
        {
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(configuration));
        }

        builder.ConfigureTestServices(services =>
        {
            // The real provider is registered as a typed HttpClient; removing every registration of
            // the port and adding the stub guarantees no request can reach the network.
            services.RemoveAll<IIntradayDataProvider>();
            services.AddSingleton(provider);
        });
    }
}
