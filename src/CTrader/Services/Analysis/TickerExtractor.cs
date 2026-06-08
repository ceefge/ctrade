using System.Text.RegularExpressions;

namespace CTrader.Services.Analysis;

/// <summary>
/// Extracts stock ticker symbols from free-text news headlines/summaries.
/// Shared by MarketAnalyzer and the RSS feed client so both apply the same
/// patterns and noise filtering.
/// </summary>
public static class TickerExtractor
{
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "CEO", "CFO", "CTO", "COO", "IPO", "SEC", "FDA", "ETF", "GDP", "CPI",
        "EPS", "PE", "AI", "US", "USA", "UK", "EU", "USD", "EUR", "GBP",
        "THE", "FOR", "AND", "NOT", "NEW", "ALL", "INC", "LTD", "LLC", "CORP",
        "NYSE", "NASDAQ", "AMEX", "HAS", "ARE", "WAS", "NOW", "TOP", "BIG",
        "UP", "ITS", "HOW", "WHY", "MAY", "CAN", "NET", "FED", "Q1", "Q2", "Q3", "Q4"
    };

    public static List<string> Extract(string? headline, string? summary)
    {
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var text in new[] { headline, summary })
        {
            if (string.IsNullOrEmpty(text)) continue;

            // $AAPL style
            foreach (Match m in Regex.Matches(text, @"\$([A-Z]{1,5})\b"))
                symbols.Add(m.Groups[1].Value);

            // (NYSE:AAPL) / (NASDAQ:AAPL) style
            foreach (Match m in Regex.Matches(text, @"\((?:NYSE|NASDAQ|AMEX):([A-Z]{1,5})\)"))
                symbols.Add(m.Groups[1].Value);

            // CAPS word immediately before a stock keyword
            foreach (Match m in Regex.Matches(text, @"\b([A-Z]{2,5})\b(?=\s+(?:stock|shares|price|rises|falls|jumps|drops|earnings|revenue|Q[1-4]))"))
            {
                if (!Noise.Contains(m.Groups[1].Value))
                    symbols.Add(m.Groups[1].Value);
            }
        }

        return symbols.Where(s => !Noise.Contains(s)).ToList();
    }
}
