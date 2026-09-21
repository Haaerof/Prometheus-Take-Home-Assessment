using System.Diagnostics.CodeAnalysis;

namespace StockApp.Core.MarketData.Models;

/// <summary>
/// A validated, normalised ticker symbol such as <c>TSLA</c>, <c>BRK-B</c> or <c>VOD.L</c>.
/// </summary>
/// <remarks>
/// Parsing a raw string into this type once, at the edge of the system, means every layer
/// beneath it can take a <see cref="Symbol"/> and know the value has already been checked.
/// </remarks>
public sealed record Symbol
{
    /// <summary>The longest symbol we accept. Real symbols are far shorter; this is an upper bound.</summary>
    public const int MaxLength = 20;

    private Symbol(string value) => Value = value;

    /// <summary>The normalised (upper-case, trimmed) symbol.</summary>
    public string Value { get; }

    /// <summary>
    /// Attempts to validate and normalise <paramref name="raw"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the symbol is well formed; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <see cref="NotNullWhenAttribute"/> tells the compiler that a <see langword="true"/> result
    /// guarantees a non-null <paramref name="symbol"/>, so callers need no null check and no
    /// null-forgiving operator after a successful call.
    /// </remarks>
    public static bool TryCreate(string? raw, [NotNullWhen(true)] out Symbol? symbol)
    {
        symbol = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalised = raw.Trim().ToUpperInvariant();

        if (normalised.Length > MaxLength || !normalised.All(IsAllowed))
        {
            return false;
        }

        symbol = new Symbol(normalised);
        return true;
    }

    /// <summary>Validates and normalises <paramref name="raw"/>, throwing if it is not well formed.</summary>
    /// <exception cref="ArgumentException">The symbol is empty, too long, or contains unsupported characters.</exception>
    public static Symbol Create(string? raw) =>
        TryCreate(raw, out var symbol)
            ? symbol
            : throw new ArgumentException($"'{raw}' is not a valid stock symbol.", nameof(raw));

    /// <summary>
    /// Characters seen in real symbols: letters and digits, plus the separators used by
    /// share classes (BRK-B), exchange suffixes (VOD.L), indices (^GSPC) and futures (ES=F).
    /// </summary>
    private static bool IsAllowed(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '^' or '=';

    /// <inheritdoc />
    public override string ToString() => Value;
}
