using System.ComponentModel.DataAnnotations;

namespace StockApp.Api.Serialization;

/// <summary>
/// How prices are reduced for publication, bound from the <c>PricePrecision</c> configuration section.
/// </summary>
/// <remarks>
/// The defaults are what the assessment specifies and what financial convention expects: four
/// decimal places, with halves rounded away from zero. Both EU Regulation 1103/97 (euro conversion)
/// and Regulation DD (US deposit disclosure) round halves up rather than to even, and neither
/// permits truncating an intermediate value.
/// <para>
/// The policy is configuration rather than a constant because it is a deployment decision, not a
/// property of the calculation: a consumer bound by a different convention can set one here without
/// the domain, the controller or the response contract changing.
/// </para>
/// </remarks>
public sealed class PricePrecisionOptions
{
    /// <summary>The configuration section these settings are bound from.</summary>
    public const string SectionName = "PricePrecision";

    /// <summary>How many decimal places published prices carry. The assessment specifies four.</summary>
    [Range(0, 10)]
    public int DecimalPlaces { get; init; } = 4;

    /// <summary>
    /// How a value that cannot be represented exactly is reduced.
    /// </summary>
    /// <remarks>
    /// <see cref="System.MidpointRounding.AwayFromZero"/> rounds halves up and is the default.
    /// <see cref="System.MidpointRounding.ToEven"/> is banker's rounding, which spreads bias across
    /// many values but is not what most published price tables use.
    /// <see cref="System.MidpointRounding.ToZero"/> truncates: every value is cut off rather than
    /// rounded, which understates prices by up to one unit in the last place, consistently in the
    /// same direction. Choose it only where a counterparty requires cut-off behaviour.
    /// </remarks>
    public MidpointRounding Rounding { get; init; } = MidpointRounding.AwayFromZero;
}
