using StockApp.Core.MarketData.Models;

namespace StockApp.Core.Tests.MarketData;

public class SymbolTests
{
    [Theory]
    [InlineData("TSLA", "TSLA")]
    [InlineData("tsla", "TSLA")]
    [InlineData("  tsla  ", "TSLA")]
    [InlineData("BRK-B", "BRK-B")]
    [InlineData("VOD.L", "VOD.L")]
    [InlineData("^GSPC", "^GSPC")]
    [InlineData("ES=F", "ES=F")]
    public void TryCreate_AcceptsAndNormalisesRealSymbols(string raw, string expected)
    {
        var accepted = Symbol.TryCreate(raw, out var symbol);

        Assert.True(accepted);
        Assert.Equal(expected, symbol!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TS LA")]        // spaces are not part of any symbol
    [InlineData("TSLA!")]        // punctuation we do not expect
    [InlineData("TSLA;DROP")]    // the shape an injection attempt takes
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    public void TryCreate_RejectsMalformedInput(string? raw)
    {
        var accepted = Symbol.TryCreate(raw, out var symbol);

        Assert.False(accepted);
        Assert.Null(symbol);
    }

    [Fact]
    public void Create_ThrowsOnMalformedInput()
    {
        var exception = Assert.Throws<ArgumentException>(() => Symbol.Create("TS LA"));

        Assert.Contains("TS LA", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Symbols_AreEqualWhenTheyNormaliseToTheSameValue()
    {
        Assert.Equal(Symbol.Create("tsla"), Symbol.Create("TSLA"));
    }
}
