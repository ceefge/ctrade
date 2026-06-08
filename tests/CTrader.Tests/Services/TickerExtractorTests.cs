using CTrader.Services.Analysis;
using FluentAssertions;
using Xunit;

namespace CTrader.Tests.Services;

public class TickerExtractorTests
{
    [Fact]
    public void Extract_FindsDollarPrefixedTickers()
    {
        var result = TickerExtractor.Extract("$AAPL surges after earnings", null);
        result.Should().Contain("AAPL");
    }

    [Fact]
    public void Extract_FindsExchangePrefixedTickers()
    {
        var result = TickerExtractor.Extract("Apple (NASDAQ:AAPL) reports record revenue", null);
        result.Should().Contain("AAPL");
    }

    [Fact]
    public void Extract_FindsCapsWordBeforeStockKeyword()
    {
        var result = TickerExtractor.Extract("TSLA stock rises 5% today", null);
        result.Should().Contain("TSLA");
    }

    [Fact]
    public void Extract_FiltersNoiseWords()
    {
        // CEO/SEC/USD etc. must not be treated as tickers even before keywords.
        var result = TickerExtractor.Extract("The CEO says USD price stable, SEC shares view", null);
        result.Should().NotContain("CEO");
        result.Should().NotContain("USD");
        result.Should().NotContain("SEC");
    }

    [Fact]
    public void Extract_Deduplicates_AcrossHeadlineAndSummary()
    {
        var result = TickerExtractor.Extract("$MSFT update", "$MSFT continues to climb");
        result.Count(s => s == "MSFT").Should().Be(1);
    }

    [Fact]
    public void Extract_ReturnsEmpty_ForNullOrPlainText()
    {
        TickerExtractor.Extract(null, null).Should().BeEmpty();
        TickerExtractor.Extract("no tickers here", "just plain words").Should().BeEmpty();
    }
}
