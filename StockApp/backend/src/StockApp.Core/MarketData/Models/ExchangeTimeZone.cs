namespace StockApp.Core.MarketData.Models;

/// <summary>
/// The clock a trading session is measured against, together with how confidently it was determined.
/// </summary>
/// <param name="ReportedId">The IANA identifier the data source reported, for example <c>America/New_York</c>.</param>
/// <param name="TimeZone">The resolved time zone used to decide which day a bar belongs to.</param>
/// <param name="IsFallback">
/// <see langword="true"/> when <paramref name="ReportedId"/> could not be resolved on this host and UTC
/// was substituted. Grouping may then split a single session across two days, so callers are expected
/// to surface this rather than present the result as authoritative.
/// </param>
public sealed record ExchangeTimeZone(string ReportedId, TimeZoneInfo TimeZone, bool IsFallback)
{
    /// <summary>The reported identifier was recognised and is being used.</summary>
    public static ExchangeTimeZone Resolved(string reportedId, TimeZoneInfo timeZone) =>
        new(reportedId, timeZone, IsFallback: false);

    /// <summary>The reported identifier was not recognised on this host; UTC is being used instead.</summary>
    public static ExchangeTimeZone FallbackToUtc(string reportedId) =>
        new(reportedId, TimeZoneInfo.Utc, IsFallback: true);
}
