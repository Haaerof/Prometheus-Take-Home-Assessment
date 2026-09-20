using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using StockApp.Api.Contracts;
using StockApp.Core.MarketData.Exceptions;

namespace StockApp.Api.ErrorHandling;

/// <summary>
/// Translates market data failures into RFC 9457 problem responses.
/// </summary>
/// <remarks>
/// The equivalent of a Spring <c>@ControllerAdvice</c>: one place that decides what each kind of
/// failure looks like to a caller, so controllers never catch infrastructure exceptions themselves.
/// Anything this handler does not recognise falls through to the default 500 response, which is the
/// correct outcome for a bug in our own code.
/// </remarks>
internal sealed partial class MarketDataExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<MarketDataExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not MarketDataException marketDataException)
        {
            return false;
        }

        var (statusCode, title, errorCode) = Describe(marketDataException);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            LogUpstreamFailure(logger, marketDataException);
        }
        else
        {
            LogRequestRejected(logger, marketDataException.Message);
        }

        if (marketDataException is UpstreamRateLimitedException { RetryAfter: { } retryAfter })
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = marketDataException,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = Detail(marketDataException),

                // A machine-readable code, so a client branches on an identifier rather than on
                // prose that may be reworded. See ApiErrorCode.
                Extensions = { ["errorCode"] = errorCode }
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps each failure to the status, title and stable code a caller sees.
    /// </summary>
    /// <remarks>
    /// An upstream contract violation is kept distinct from a transport failure even though both are
    /// 502s: one means the data source has changed shape and needs our attention, the other means it
    /// was briefly unreachable and a retry may succeed. Clients act on those differently.
    /// </remarks>
    private static (int StatusCode, string Title, string ErrorCode) Describe(MarketDataException exception) =>
        exception switch
        {
            SymbolNotFoundException => (
                StatusCodes.Status404NotFound, "Symbol not found", ApiErrorCode.SymbolNotFound),
            UpstreamRateLimitedException => (
                StatusCodes.Status503ServiceUnavailable, "Market data source is busy", ApiErrorCode.UpstreamRateLimited),
            UpstreamContractException => (
                StatusCodes.Status502BadGateway, "Market data source changed unexpectedly", ApiErrorCode.UpstreamContract),
            _ => (
                StatusCodes.Status502BadGateway, "Market data source unavailable", ApiErrorCode.UpstreamUnavailable)
        };

    /// <summary>
    /// Chooses what to tell the caller. The upstream's own wording is passed on where it is useful
    /// and safe; otherwise callers get a general message and the detail stays in the logs.
    /// </summary>
    private static string Detail(MarketDataException exception) => exception switch
    {
        SymbolNotFoundException notFound =>
            notFound.UpstreamDescription ?? notFound.Message,
        UpstreamRateLimitedException =>
            "The market data source is rate limiting requests. Please retry shortly.",
        UpstreamContractException =>
            "The market data source returned data this application could not interpret.",
        _ =>
            "The market data source could not be reached. Please try again later."
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Market data request failed upstream.")]
    private static partial void LogUpstreamFailure(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Market data request rejected: {Reason}")]
    private static partial void LogRequestRejected(ILogger logger, string reason);
}
