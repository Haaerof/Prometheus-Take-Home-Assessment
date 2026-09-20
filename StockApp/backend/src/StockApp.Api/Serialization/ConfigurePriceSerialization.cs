using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace StockApp.Api.Serialization;

/// <summary>
/// Applies the configured price precision to the JSON serializer the web layer uses.
/// </summary>
/// <remarks>
/// Serializer settings are configured through the options system rather than built in
/// <c>Program</c> so that <see cref="PricePrecisionOptions"/> is validated on start and resolved
/// through dependency injection like any other setting. A converter attached by attribute could not
/// be given configuration, since the serializer constructs those itself.
/// </remarks>
internal sealed class ConfigurePriceSerialization(IOptions<PricePrecisionOptions> pricePrecision)
    : IConfigureOptions<JsonOptions>
{
    /// <inheritdoc />
    public void Configure(JsonOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.JsonSerializerOptions.Converters.Add(new PriceJsonConverter(pricePrecision.Value));
    }
}
