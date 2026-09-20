using Microsoft.AspNetCore.Mvc;
using StockApp.Api.Contracts;
using StockApp.Core.MarketData.Abstractions;
using StockApp.Core.MarketData.Models;

namespace StockApp.Api.Controllers;

/// <summary>
/// Endpoints for stock market data.
/// </summary>
[ApiController]
[Route("api/v1/stocks")]
public sealed class StocksController(IDailySummaryService dailySummaryService) : ControllerBase
{
    /// <summary>
    /// Returns one entry per trading day for the last month, averaging each day's interval lows and
    /// highs and totalling its volume.
    /// </summary>
    /// <param name="symbol">The stock symbol, for example <c>TSLA</c>.</param>
    /// <param name="cancellationToken">Cancelled when the caller disconnects.</param>
    /// <response code="200">Daily summaries, oldest first. Empty when the symbol has no recent trading.</response>
    /// <response code="400">The symbol is not well formed.</response>
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
        CancellationToken cancellationToken)
    {
        if (!Symbol.TryCreate(symbol, out var parsedSymbol))
        {
            // Built through the factory so this response carries the same type and traceId members
            // as the ones the exception handler writes, and the same machine-readable error code.
            var problem = ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid symbol",
                detail: $"'{symbol}' is not a valid stock symbol.");

            problem.Extensions["errorCode"] = ApiErrorCode.InvalidSymbol;

            // BadRequest(problem) would serialise it as application/json. Problem documents have
            // their own media type, and clients are entitled to rely on it.
            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" }
            };
        }

        var report = await dailySummaryService
            .GetLastMonthAsync(parsedSymbol, cancellationToken)
            .ConfigureAwait(false);

        // The response body is fixed by the published contract, so the clock used for grouping is
        // reported in headers. A fallback means days may not line up with real trading sessions,
        // which clients are expected to surface rather than present as authoritative.
        Response.Headers["X-Grouping-Timezone"] = report.ExchangeTimeZone.ReportedId;
        Response.Headers["X-Grouping-Timezone-Fallback"] =
            report.ExchangeTimeZone.IsFallback ? "true" : "false";

        return Ok(report.Days.Select(DailySummaryResponse.From).ToList());
    }
}
