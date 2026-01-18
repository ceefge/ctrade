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

    private class LlmRegimeResponse
    {
        public string? Regime { get; set; }
        public decimal Confidence { get; set; }
        public string? RecommendedStrategy { get; set; }
        public string? Reasoning { get; set; }
        public string? RiskLevel { get; set; }
    }
}
