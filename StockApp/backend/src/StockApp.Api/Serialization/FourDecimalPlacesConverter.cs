using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockApp.Api.Serialization;

/// <summary>
/// Writes a decimal with exactly four decimal places, as the published contract requires.
/// </summary>
/// <remarks>
/// Rounding alone is not enough: a decimal remembers its scale, and rounding only ever reduces it,
/// so <c>Math.Round(40.29m, 4)</c> still serialises as <c>40.29</c>. Formatting to a fixed number of
/// places is what produces <c>40.2900</c>. The value is written raw so it remains a JSON number
/// rather than becoming a quoted string.
/// </remarks>
internal sealed class FourDecimalPlacesConverter : JsonConverter<decimal>
{
    private const int DecimalPlaces = 4;

    /// <inheritdoc />
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDecimal();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Rounding is explicit rather than left to the formatter: Math.Round defaults to banker's
        // rounding in .NET, which is not what "rounded to four places" conventionally means here.
        var rounded = Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);

        writer.WriteRawValue(rounded.ToString("F4", CultureInfo.InvariantCulture));
    }
}
