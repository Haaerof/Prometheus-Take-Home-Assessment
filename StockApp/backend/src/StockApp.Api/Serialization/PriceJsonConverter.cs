using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockApp.Api.Serialization;

/// <summary>
/// Writes prices at the configured precision, so every published figure is reduced the same way.
/// </summary>
/// <remarks>
/// Rounding alone would not be enough to honour a fixed number of decimal places: a decimal carries
/// its own scale and rounding only ever reduces it, so <c>Math.Round(40.29m, 4)</c> still writes
/// <c>40.29</c>. Formatting to a fixed width is what produces <c>40.2900</c>. The result is written
/// raw so it stays a JSON number rather than becoming a quoted string.
/// </remarks>
internal sealed class PriceJsonConverter(PricePrecisionOptions options) : JsonConverter<decimal>
{
    private readonly string _format = "F" + options.DecimalPlaces.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions serializerOptions) =>
        reader.GetDecimal();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Rounding is applied explicitly rather than left to the formatter, so the configured
        // strategy governs the result instead of the framework's default of rounding to even.
        var reduced = Math.Round(value, options.DecimalPlaces, options.Rounding);

        writer.WriteRawValue(reduced.ToString(_format, CultureInfo.InvariantCulture));
    }
}
