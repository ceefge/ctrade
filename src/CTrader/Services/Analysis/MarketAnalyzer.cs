using System.Text;
using System.Text.Json;
using CTrader.Data;
using CTrader.Data.Entities;
using CTrader.Models;
using CTrader.Services.Analysis.LlmClients;
using Microsoft.EntityFrameworkCore;

namespace CTrader.Services.Analysis;

public class MarketAnalyzer : IMarketAnalyzer
{
    private readonly ILlmClient _llmClient;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<MarketAnalyzer> _logger;

    private const string RegimeAnalysisSystemPrompt = @"You are a market regime analyst. Your task is to analyze financial news and market data to determine the current market regime.

Market regimes:
- RiskOn: Bullish sentiment, positive news, low volatility, favorable economic conditions
- RiskOff: Bearish sentiment, negative news, flight to safety, economic concerns
- Neutral: Mixed signals, sideways market, no clear direction
- HighVolatility: Elevated VIX (>20), significant uncertainty, rapid price movements
- Crisis: Extreme fear, VIX >30, major negative events, potential systemic risk

Your response must be a JSON object with this exact structure:
{
    ""regime"": ""RiskOn|RiskOff|Neutral|HighVolatility|Crisis"",
    ""confidence"": 0.0 to 1.0,
    ""recommendedStrategy"": ""brief strategy recommendation"",
    ""reasoning"": ""brief explanation of the regime assessment"",
    ""riskLevel"": ""Low|Medium|High|Extreme""
}";

    private const string SummarySystemPrompt = @"You are a financial news analyst. Provide a concise summary of the market news, highlighting key themes, potential market-moving events, and overall sentiment. Keep the summary to 2-3 paragraphs.";

    private const string StockRankingSystemPrompt = @"Du bist ein erfahrener Finanzanalyst. Bewerte die folgenden Aktien anhand der aggregierten Nachrichtendaten.

Bewertungskriterien:
- Nachrichtendynamik: Viele Erwähnungen = hohe Aufmerksamkeit
- Sentiment: Positives Sentiment deutet auf Chancen, negatives auf Risiken
- Katalysator-Events: Übernahmen, Earnings, FDA-Entscheidungen, etc.

Antworte ausschließlich als JSON-Objekt mit dieser Struktur:
{
    ""stocks"": [
        {
            ""symbol"": ""AAPL"",
            ""companyName"": ""Apple"",
            ""score"": 0.85,
            ""signal"": ""Bullish"",
            ""reasoning"": ""Kurze Begründung auf Deutsch""
        }
    ]
}

Regeln:
- score: 0.0 bis 1.0 (0=uninteressant, 1=sehr interessant für Trading)
- signal: exakt ""Bullish"", ""Bearish"" oder ""Neutral""
- companyName: offizieller Kurzname der Firma (z.B. ""Apple"", ""Microsoft"", ""Tesla"")
- reasoning: maximal 1 Satz auf Deutsch
- Sortiere nach score absteigend";

    public MarketAnalyzer(ILlmClient llmClient, IDbContextFactory<AppDbContext> contextFactory, ILogger<MarketAnalyzer> logger)
    {
        _llmClient = llmClient;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<RegimeAnalysisResult> AnalyzeMarketRegimeAsync(IEnumerable<MarketNews> news, decimal? vix = null, CancellationToken cancellationToken = default)
    {
        var newsText = FormatNewsForAnalysis(news.Take(20));
        var userPrompt = new StringBuilder();

        userPrompt.AppendLine("Analyze the following market data and news to determine the current market regime:");
        userPrompt.AppendLine();

        if (vix.HasValue)
        {
            userPrompt.AppendLine($"Current VIX: {vix:F2}");
            userPrompt.AppendLine();
        }

        userPrompt.AppendLine("Recent News Headlines:");
        userPrompt.AppendLine(newsText);

        try
        {
            var response = await _llmClient.CompleteJsonAsync<LlmRegimeResponse>(RegimeAnalysisSystemPrompt, userPrompt.ToString(), cancellationToken);

            if (response == null)
            {
                _logger.LogWarning("Failed to parse regime analysis response, using fallback");
                return CreateFallbackResult(vix);
            }

            var result = new RegimeAnalysisResult
            {
                Regime = ParseRegime(response.Regime),
                Confidence = response.Confidence,
                RecommendedStrategy = response.RecommendedStrategy,
                Reasoning = response.Reasoning,
                RiskLevel = response.RiskLevel,
                AnalyzedAt = DateTime.UtcNow
            };

            // Save to database
            await SaveAnalysisAsync(result, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing market regime");
            return CreateFallbackResult(vix);
        }
    }

    public async Task<string> GetMarketSummaryAsync(IEnumerable<MarketNews> news, CancellationToken cancellationToken = default)
    {
        var newsText = FormatNewsForAnalysis(news.Take(30));
        var userPrompt = $"Please summarize the following market news:\n\n{newsText}";

        try
        {
            return await _llmClient.CompleteAsync(SummarySystemPrompt, userPrompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating market summary");
            return "Unable to generate market summary at this time.";
        }
    }

    private static string FormatNewsForAnalysis(IEnumerable<MarketNews> news)
    {
        var sb = new StringBuilder();
        foreach (var item in news)
        {
            sb.AppendLine($"- [{item.PublishedAt:MM/dd HH:mm}] {item.Headline}");
            if (!string.IsNullOrEmpty(item.Summary))
            {
                var summary = item.Summary.Length > 200 ? item.Summary[..200] + "..." : item.Summary;
                sb.AppendLine($"  {summary}");
            }
        }
        return sb.ToString();
    }

    private static MarketRegime ParseRegime(string? regime)
    {
        return regime?.ToLowerInvariant() switch
        {
            "riskon" => MarketRegime.RiskOn,
            "riskoff" => MarketRegime.RiskOff,
            "neutral" => MarketRegime.Neutral,
            "highvolatility" => MarketRegime.HighVolatility,
            "crisis" => MarketRegime.Crisis,
            _ => MarketRegime.Unknown
        };
    }

    private static RegimeAnalysisResult CreateFallbackResult(decimal? vix)
    {
        var regime = MarketRegime.Neutral;
        var riskLevel = "Medium";

        if (vix.HasValue)
        {
            if (vix > 30)
            {
                regime = MarketRegime.Crisis;
                riskLevel = "Extreme";
            }
            else if (vix > 20)
            {
                regime = MarketRegime.HighVolatility;
                riskLevel = "High";
            }
            else if (vix < 15)
            {
                regime = MarketRegime.RiskOn;
                riskLevel = "Low";
            }
        }

        return new RegimeAnalysisResult
        {
            Regime = regime,
            Confidence = 0.5m,
            RecommendedStrategy = "Conservative approach due to analysis unavailable",
            Reasoning = "Fallback analysis based on VIX level",
            RiskLevel = riskLevel,
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private async Task SaveAnalysisAsync(RegimeAnalysisResult result, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.RegimeAnalyses.Add(new RegimeAnalysis
        {
            Regime = result.Regime.ToString(),
            Confidence = result.Confidence,
            RecommendedStrategy = result.RecommendedStrategy,
            Reasoning = result.Reasoning,
            RiskLevel = result.RiskLevel,
            AnalyzedAt = result.AnalyzedAt
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static readonly System.Text.RegularExpressions.Regex TickerRegex = new(
        @"(?:\((?:NYSE|NASDAQ|AMEX):)?(\b[A-Z]{1,5}\b)(?:\))?",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static List<string> ExtractSymbolsFromText(string? headline, string? summary)
    {
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var noise = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CEO", "CFO", "CTO", "COO", "IPO", "SEC", "FDA", "ETF", "GDP", "CPI",
            "EPS", "PE", "AI", "US", "USA", "UK", "EU", "USD", "EUR", "GBP",
            "THE", "FOR", "AND", "NOT", "NEW", "ALL", "INC", "LTD", "LLC", "CORP",
            "NYSE", "NASDAQ", "AMEX", "HAS", "ARE", "WAS", "NOW", "TOP", "BIG",
            "UP", "ITS", "HOW", "WHY", "MAY", "CAN", "NET", "FED", "Q1", "Q2", "Q3", "Q4"
        };

        foreach (var text in new[] { headline, summary })
        {
            if (string.IsNullOrEmpty(text)) continue;

            // Match $AAPL style
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\$([A-Z]{1,5})\b"))
                symbols.Add(m.Groups[1].Value);

            // Match (NYSE:AAPL) or (NASDAQ:AAPL) style
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\((?:NYSE|NASDAQ|AMEX):([A-Z]{1,5})\)"))
                symbols.Add(m.Groups[1].Value);

            // Match CAPS words near stock keywords
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\b([A-Z]{2,5})\b(?=\s+(?:stock|shares|price|rises|falls|jumps|drops|earnings|revenue|Q[1-4]))"))
            {
                if (!noise.Contains(m.Groups[1].Value))
                    symbols.Add(m.Groups[1].Value);
            }
        }

        return symbols.Where(s => !noise.Contains(s)).ToList();
    }

    public async Task<StockRankingResult> GenerateStockRankingAsync(IEnumerable<MarketNews> news, int topN = 10, CancellationToken cancellationToken = default)
    {
        var newsList = news.ToList();

        // Enrich: extract symbols from headlines for articles without explicit symbols
        foreach (var article in newsList.Where(n => n.Symbols.Count == 0))
        {
            var extracted = ExtractSymbolsFromText(article.Headline, article.Summary);
            if (extracted.Count > 0)
                article.Symbols = extracted;
        }

        // Pre-aggregate: group by symbol
        var symbolGroups = newsList
            .Where(n => n.Symbols.Count > 0)
            .SelectMany(n => n.Symbols.Select(s => new { Symbol = s.ToUpperInvariant(), News = n }))
            .GroupBy(x => x.Symbol)
            .Select(g => new
            {
                Symbol = g.Key,
                MentionCount = g.Count(),
                AvgSentiment = g.Where(x => x.News.SentimentScore.HasValue)
                                .Select(x => x.News.SentimentScore!.Value)
                                .DefaultIfEmpty(0m)
                                .Average(),
                TopHeadlines = g.OrderByDescending(x => x.News.PublishedAt)
                                .Take(3)
                                .Select(x => x.News.Headline)
                                .ToList()
            })
            .OrderByDescending(x => x.MentionCount)
            .ThenByDescending(x => Math.Abs(x.AvgSentiment))
            .Take(20)
            .ToList();

        if (symbolGroups.Count == 0)
        {
            return new StockRankingResult
            {
                Stocks = [],
                AnalyzedAt = DateTime.UtcNow,
                NewsArticlesAnalyzed = newsList.Count
            };
        }

        // Build compact prompt for LLM
        var sb = new StringBuilder();
        sb.AppendLine($"Analysiere diese {symbolGroups.Count} Aktien basierend auf {newsList.Count} Nachrichtenartikeln:");
        sb.AppendLine();
        foreach (var g in symbolGroups)
        {
            sb.AppendLine($"## {g.Symbol} (Erwähnungen: {g.MentionCount}, Ø-Sentiment: {g.AvgSentiment:F2})");
            foreach (var headline in g.TopHeadlines)
                sb.AppendLine($"  - {headline}");
        }
        sb.AppendLine();
        sb.AppendLine($"Wähle die Top {topN} interessantesten Aktien aus und bewerte sie.");

        // Build lookup for merge
        var symbolLookup = symbolGroups.ToDictionary(g => g.Symbol, g => new { g.MentionCount, g.AvgSentiment });

        try
        {
            var response = await _llmClient.CompleteJsonAsync<LlmRankingResponse>(StockRankingSystemPrompt, sb.ToString(), cancellationToken);

            if (response?.Stocks is { Count: > 0 })
            {
                var ranked = response.Stocks
                    .Take(topN)
                    .Select((s, i) =>
                    {
                        var symbol = s.Symbol?.ToUpperInvariant() ?? "";
                        symbolLookup.TryGetValue(symbol, out var lookup);
                        return new RankedStock
                        {
                            Rank = i + 1,
                            Symbol = symbol,
                            CompanyName = s.CompanyName ?? TickerNames.GetName(symbol) ?? "",
                            Score = Math.Clamp(s.Score, 0m, 1m),
                            Reasoning = s.Reasoning ?? "",
                            Signal = NormalizeSignal(s.Signal),
                            MentionCount = lookup?.MentionCount ?? 0,
                            AvgSentiment = lookup?.AvgSentiment
                        };
                    })
                    .ToList();

                return new StockRankingResult
                {
                    Stocks = ranked,
                    AnalyzedAt = DateTime.UtcNow,
                    NewsArticlesAnalyzed = newsList.Count
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM stock ranking failed, using fallback");
        }

        // Fallback: rank purely by mention count + sentiment
        return CreateFallbackRanking(symbolGroups.Select(g => new FallbackSymbol(g.Symbol, g.MentionCount, g.AvgSentiment)), topN, newsList.Count);
    }

    private const string StockDetailSystemPrompt = @"Du bist ein erfahrener Finanzanalyst. Analysiere die gegebene Aktie anhand der bereitgestellten Nachrichtenartikel.

Antworte ausschließlich als JSON-Objekt mit dieser Struktur:
{
    ""technical"": {
        ""trend"": ""Aufwärts|Abwärts|Seitwärts"",
        ""signalSummary"": ""Zusammenfassung der technischen Signale (1-2 Sätze)"",
        ""indicators"": [""Indikator 1: Bewertung"", ""Indikator 2: Bewertung""],
        ""outlook"": ""Technischer Ausblick (1-2 Sätze)"",
        ""riskLevel"": ""Niedrig|Mittel|Hoch""
    },
    ""fundamental"": {
        ""companyProfile"": ""Kurze Firmenbeschreibung (1-2 Sätze)"",
        ""keyMetrics"": [""KGV: ~XX"", ""Marktkapitalisierung: ~XXX Mrd.""],
        ""financialHealth"": ""Bewertung der finanziellen Gesundheit (1-2 Sätze)"",
        ""growthOutlook"": ""Wachstumsaussichten (1-2 Sätze)"",
        ""catalysts"": [""Katalysator 1"", ""Katalysator 2""]
    }
}

Regeln:
- Alle Texte auf Deutsch
- Basiere die Analyse auf den News-Artikeln und allgemeinem Wissen über die Firma
- Sei konkret und nenne Zahlen wo möglich
- indicators: 3-5 technische Indikatoren/Signale
- keyMetrics: 3-5 wichtige Kennzahlen
- catalysts: 2-4 kommende Katalysatoren/Events";

    public async Task<StockDetailResult> AnalyzeStockDetailAsync(string symbol, IEnumerable<MarketNews> news, decimal? currentPrice = null, CancellationToken cancellationToken = default)
    {
        var newsList = news.ToList();
        var companyName = TickerNames.GetName(symbol) ?? symbol;

        // Build news mentions from data (no LLM needed)
        var newsMentions = new NewsMentions
        {
            TotalMentions = newsList.Count,
            AverageSentiment = newsList.Where(n => n.SentimentScore.HasValue)
                .Select(n => n.SentimentScore!.Value)
                .DefaultIfEmpty(0m)
                .Average(),
            RecentArticles = newsList
                .OrderByDescending(n => n.PublishedAt)
                .Take(20)
                .Select(n => new NewsItem
                {
                    Headline = n.Headline,
                    Source = n.Source,
                    Url = n.Url,
                    PublishedAt = n.PublishedAt,
                    Sentiment = n.SentimentScore
                })
                .ToList()
        };

        // Determine sentiment trend
        if (newsList.Count >= 4)
        {
            var half = newsList.Count / 2;
            var sorted = newsList.OrderBy(n => n.PublishedAt).ToList();
            var olderAvg = sorted.Take(half).Where(n => n.SentimentScore.HasValue).Select(n => n.SentimentScore!.Value).DefaultIfEmpty(0m).Average();
            var newerAvg = sorted.Skip(half).Where(n => n.SentimentScore.HasValue).Select(n => n.SentimentScore!.Value).DefaultIfEmpty(0m).Average();
            newsMentions.SentimentTrend = (newerAvg - olderAvg) switch
            {
                > 0.1m => "Steigend",
                < -0.1m => "Fallend",
                _ => "Stabil"
            };
        }

        // Build compact news text for LLM
        var sb = new StringBuilder();
        sb.AppendLine($"Aktie: {symbol} ({companyName})");
        if (currentPrice.HasValue)
            sb.AppendLine($"Aktueller Kurs: ${currentPrice:F2}");
        sb.AppendLine($"\nAnzahl relevanter Artikel: {newsList.Count}");
        sb.AppendLine($"Ø-Sentiment: {newsMentions.AverageSentiment:F2}");
        sb.AppendLine($"\nAktuelle Nachrichten:");
        foreach (var article in newsList.OrderByDescending(n => n.PublishedAt).Take(15))
        {
            sb.AppendLine($"- [{article.PublishedAt:dd.MM HH:mm}] {article.Headline}");
            if (!string.IsNullOrEmpty(article.Summary))
            {
                var summary = article.Summary.Length > 150 ? article.Summary[..150] + "..." : article.Summary;
                sb.AppendLine($"  {summary}");
            }
        }

        var result = new StockDetailResult
        {
            Symbol = symbol,
            CompanyName = companyName,
            CurrentPrice = currentPrice,
            AnalyzedAt = DateTime.UtcNow,
            News = newsMentions
        };

        try
        {
            var response = await _llmClient.CompleteJsonAsync<LlmStockDetailResponse>(StockDetailSystemPrompt, sb.ToString(), cancellationToken);

            if (response?.Technical != null)
            {
                result.Technical = new TechnicalAnalysis
                {
                    Trend = response.Technical.Trend ?? "Neutral",
                    SignalSummary = response.Technical.SignalSummary ?? "",
                    Indicators = response.Technical.Indicators ?? [],
                    Outlook = response.Technical.Outlook ?? "",
                    RiskLevel = response.Technical.RiskLevel ?? "Mittel"
                };
            }

            if (response?.Fundamental != null)
            {
                result.Fundamental = new FundamentalAnalysis
                {
                    CompanyProfile = response.Fundamental.CompanyProfile ?? "",
                    KeyMetrics = response.Fundamental.KeyMetrics ?? [],
                    FinancialHealth = response.Fundamental.FinancialHealth ?? "",
                    GrowthOutlook = response.Fundamental.GrowthOutlook ?? "",
                    Catalysts = response.Fundamental.Catalysts ?? []
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM stock detail analysis failed for {Symbol}", symbol);
            result.Technical.SignalSummary = "Analyse konnte nicht durchgeführt werden.";
            result.Fundamental.CompanyProfile = $"{companyName} — Detailanalyse derzeit nicht verfügbar.";
        }

        return result;
    }

    private static string NormalizeSignal(string? signal) => signal?.ToLowerInvariant() switch
    {
        "bullish" => "Bullish",
        "bearish" => "Bearish",
        _ => "Neutral"
    };

    private static StockRankingResult CreateFallbackRanking(IEnumerable<FallbackSymbol> symbols, int topN, int totalArticles)
    {
        var ranked = symbols
            .OrderByDescending(s => s.MentionCount)
            .ThenByDescending(s => Math.Abs(s.AvgSentiment))
            .Take(topN)
            .Select((s, i) => new RankedStock
            {
                Rank = i + 1,
                Symbol = s.Symbol,
                CompanyName = TickerNames.GetName(s.Symbol) ?? "",
                Score = Math.Clamp(s.MentionCount / 10m + Math.Abs(s.AvgSentiment), 0m, 1m),
                Reasoning = "Ranking basiert auf Erwähnungshäufigkeit und Sentiment (ohne KI-Analyse)",
                Signal = s.AvgSentiment > 0.1m ? "Bullish" : s.AvgSentiment < -0.1m ? "Bearish" : "Neutral",
                MentionCount = s.MentionCount,
                AvgSentiment = s.AvgSentiment
            })
            .ToList();

        return new StockRankingResult
        {
            Stocks = ranked,
            AnalyzedAt = DateTime.UtcNow,
            NewsArticlesAnalyzed = totalArticles
        };
    }

    private record FallbackSymbol(string Symbol, int MentionCount, decimal AvgSentiment);

    private class LlmRegimeResponse
    {
        public string? Regime { get; set; }
        public decimal Confidence { get; set; }
        public string? RecommendedStrategy { get; set; }
        public string? Reasoning { get; set; }
        public string? RiskLevel { get; set; }
    }

    private class LlmRankingResponse
    {
        public List<LlmRankedStock>? Stocks { get; set; }
    }

    private class LlmRankedStock
    {
        public string? Symbol { get; set; }
        public string? CompanyName { get; set; }
        public decimal Score { get; set; }
        public string? Signal { get; set; }
        public string? Reasoning { get; set; }
    }

    private class LlmStockDetailResponse
    {
        public LlmTechnical? Technical { get; set; }
        public LlmFundamental? Fundamental { get; set; }
    }

    private class LlmTechnical
    {
        public string? Trend { get; set; }
        public string? SignalSummary { get; set; }
        public List<string>? Indicators { get; set; }
        public string? Outlook { get; set; }
        public string? RiskLevel { get; set; }
    }

    private class LlmFundamental
    {
        public string? CompanyProfile { get; set; }
        public List<string>? KeyMetrics { get; set; }
        public string? FinancialHealth { get; set; }
        public string? GrowthOutlook { get; set; }
        public List<string>? Catalysts { get; set; }
    }
}
