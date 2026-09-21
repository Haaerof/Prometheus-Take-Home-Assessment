using System.Globalization;
using StockApp.Api.Tests.TestDoubles;
using StockApp.Core.MarketData.Models;

namespace StockApp.Api.Tests.Endpoints;

/// <summary>
/// Covers the headers a browser is allowed to read.
/// </summary>
/// <remarks>
/// A browser hides every response header from JavaScript unless the server names it in
/// <c>Access-Control-Expose-Headers</c>. The context this API reports beside the body — which clock
/// grouped the days, and which exchange they came from — is therefore only usable if it is listed
/// here, so the omission is worth a test rather than a comment.
/// </remarks>
public class CorsTests
{
    private const string FrontendOrigin = "http://localhost:5173";

    [Theory]
    [InlineData("X-Grouping-Timezone")]
    [InlineData("X-Grouping-Timezone-Fallback")]
    [InlineData("X-Exchange-Name")]
    public async Task ContextHeaders_AreReadableByTheBrowser(string header)
    {
        using var factory = new StockApiFactory(
            new StubIntradayDataProvider(Series()),
            new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = FrontendOrigin });
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/api/v1/stocks/TSLA/daily", UriKind.Relative));
        request.Headers.Add("Origin", FrontendOrigin);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var exposed = response.Headers.GetValues("Access-Control-Expose-Headers").First();

        Assert.Contains(header, exposed, StringComparison.Ordinal);
    }

    private static IntradaySeries Series() =>
        new(Symbol.Create("TSLA"),
            ExchangeTimeZone.Resolved("America/New_York", TimeZoneInfo.FindSystemTimeZoneById("America/New_York")),
            [new IntradayBar(DateTimeOffset.Parse("2026-09-14T14:30:00Z", CultureInfo.InvariantCulture), 1m, 2m, 3L)],
            "NasdaqGS");
}
