using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockApp.Api.Contracts;
using StockApp.Api.Serialization;
using StockApp.Core.MarketData.Abstractions;
using StockApp.Core.MarketData.Models;

namespace StockApp.Api.Controllers;

/// <summary>
/// Endpoints for stock market data.
/// </summary>
[ApiController]
[Route("api/v1/stocks")]
public sealed class StocksController(
    IDailySummaryService dailySummaryService,
    IOptions<PricePrecisionOptions> pricePrecision) : ControllerBase
{
    /// <summary>
    /// Returns one entry per trading day for the last month, averaging each day's interval lows and
    /// highs and totalling its volume.
    /// </summary>
    /// <param name="symbol">The stock symbol, for example <c>TSLA</c>.</param>
    /// <param name="rounding">
    /// How prices are reduced to the published precision, overriding the configured default for this
    /// request. Accepts any <see cref="MidpointRounding"/> name: <c>AwayFromZero</c> rounds halves up,
    /// <c>ToEven</c> is banker's rounding, and <c>ToZero</c> truncates.
    /// </param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <response code="200">Daily summaries, oldest first. Empty when the symbol has no recent trading.</response>
    /// <response code="400">The symbol or the rounding strategy is not well formed.</response>
    /// <response code="404">The market data source does not recognise the symbol.</response>
    /// <response code="502">The market data source failed or replied unexpectedly.</response>
    /// <response code="503">The market data source is rate limiting requests.</response>
    // Deliberately no [Produces]: it overrides a result's own content type, which would relabel
    // problem documents as application/json. The response types below describe both outcomes.
    [HttpGet("{symbol}/daily")]
    [ProducesResponseType<IReadOnlyList<DailySummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<DailySummaryResponse>>> GetDailySummaries(
        string symbol,
        [FromQuery] string? rounding,
        CancellationToken cancellationToken)
    {
        if (!Symbol.TryCreate(symbol, out var parsedSymbol))
        {
            return Invalid(
                ApiErrorCode.InvalidSymbol,
                "Invalid symbol",
                $"'{symbol}' is not a valid stock symbol.");
        }

        if (!TryResolveRounding(rounding, out var effectiveRounding))
        {
            return Invalid(
                ApiErrorCode.InvalidRounding,
                "Invalid rounding strategy",
                $"'{rounding}' is not a supported rounding strategy. Use one of: {SupportedRoundingNames}.");
        }

        var report = await dailySummaryService
            .GetLastMonthAsync(parsedSymbol, cancellationToken)
            .ConfigureAwait(false);

        WriteGroupingHeaders(report);

        var decimalPlaces = pricePrecision.Value.DecimalPlaces;

        return Ok(report.Days
            .Select(day => DailySummaryResponse.From(day, decimalPlaces, effectiveRounding))
            .ToList());
    }

    private static string SupportedRoundingNames => string.Join(", ", Enum.GetNames<MidpointRounding>());

    /// <summary>
    /// Resolves the rounding strategy for this request, falling back to the configured default when
    /// the caller does not ask for one.
    /// </summary>
    private bool TryResolveRounding(string? requested, out MidpointRounding rounding)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            rounding = pricePrecision.Value.Rounding;
            return true;
        }

        rounding = pricePrecision.Value.Rounding;

        // Matched against the known names, not merely parsed: Enum.TryParse also accepts numeric
        // text, so "3" would otherwise be silently honoured as whichever strategy holds that value.
        return Enum.GetNames<MidpointRounding>().Contains(requested, StringComparer.OrdinalIgnoreCase)
            && Enum.TryParse(requested, ignoreCase: true, out rounding);
    }

    /// <summary>
    /// Reports how the days were grouped, and on which exchange, in headers.
    /// </summary>
    /// <remarks>
    /// The response body is fixed by the published contract, so this context travels beside it. A
    /// fallback means days may not line up with real trading sessions, which clients are expected to
    /// surface rather than present as authoritative.
    /// </remarks>
    private void WriteGroupingHeaders(DailySummaryReport report)
    {
        Response.Headers["X-Grouping-Timezone"] = report.ExchangeTimeZone.ReportedId;
        Response.Headers["X-Grouping-Timezone-Fallback"] = report.ExchangeTimeZone.IsFallback ? "true" : "false";

        if (!string.IsNullOrWhiteSpace(report.ExchangeName))
        {
            Response.Headers["X-Exchange-Name"] = report.ExchangeName;
        }
    }

    /// <summary>
    /// Builds a 400 that matches the problem documents the exception handler writes, so a client has
    /// one error shape to parse regardless of which layer rejected the request.
    /// </summary>
    private ObjectResult Invalid(string errorCode, string title, string detail)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: StatusCodes.Status400BadRequest,
            title: title,
            detail: detail);

        problem.Extensions["errorCode"] = errorCode;

        // BadRequest(problem) would serialise it as application/json. Problem documents have their
        // own media type, and clients are entitled to rely on it.
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }
}
