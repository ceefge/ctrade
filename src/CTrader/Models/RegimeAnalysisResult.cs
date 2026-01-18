namespace CTrader.Models;

public class RegimeAnalysisResult
{
    public MarketRegime Regime { get; set; }
    public decimal Confidence { get; set; }
    public string? RecommendedStrategy { get; set; }
    public string? Reasoning { get; set; }
    public string? RiskLevel { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}
