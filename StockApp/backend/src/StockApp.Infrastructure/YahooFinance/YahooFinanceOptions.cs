using System.ComponentModel.DataAnnotations;

namespace StockApp.Infrastructure.YahooFinance;

/// <summary>
/// Settings for the Yahoo Finance chart API, bound from the <c>YahooFinance</c> configuration section.
/// </summary>
/// <remarks>
/// Validated at startup rather than on first use, so a misconfigured deployment fails immediately
/// and visibly instead of returning errors once traffic arrives.
/// </remarks>
public sealed class YahooFinanceOptions
{
    /// <summary>The configuration section these settings are bound from.</summary>
    public const string SectionName = "YahooFinance";

    /// <summary>Base address of the chart endpoint, including a trailing slash.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; init; } = "https://query1.finance.yahoo.com/v8/finance/chart/";

    /// <summary>
    /// The user agent sent with every request. Yahoo answers requests without one with
    /// <c>429 Too Many Requests</c>, so this is required rather than cosmetic.
    /// </summary>
    [Required]
    public string UserAgent { get; init; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

    /// <summary>How long a single attempt may take before it is abandoned.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
