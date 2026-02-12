namespace CTrader.Models;

public class StockRankingResult
{
    public List<RankedStock> Stocks { get; set; } = [];
    public DateTime AnalyzedAt { get; set; }
    public int NewsArticlesAnalyzed { get; set; }
}

public class RankedStock
{
    public int Rank { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string Signal { get; set; } = "Neutral";
    public int MentionCount { get; set; }
    public decimal? AvgSentiment { get; set; }
}
