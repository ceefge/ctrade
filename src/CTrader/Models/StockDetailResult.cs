namespace CTrader.Models;

public class StockDetailResult
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal? CurrentPrice { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public TechnicalAnalysis Technical { get; set; } = new();
    public FundamentalAnalysis Fundamental { get; set; } = new();
    public NewsMentions News { get; set; } = new();
}

public class TechnicalAnalysis
{
    public string Trend { get; set; } = "Neutral";
    public string SignalSummary { get; set; } = string.Empty;
    public List<string> Indicators { get; set; } = [];
    public string Outlook { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "Mittel";
}

public class FundamentalAnalysis
{
    public string CompanyProfile { get; set; } = string.Empty;
    public List<string> KeyMetrics { get; set; } = [];
    public string FinancialHealth { get; set; } = string.Empty;
    public string GrowthOutlook { get; set; } = string.Empty;
    public List<string> Catalysts { get; set; } = [];
}

public class NewsMentions
{
    public int TotalMentions { get; set; }
    public decimal AverageSentiment { get; set; }
    public string SentimentTrend { get; set; } = "Stabil";
    public List<NewsItem> RecentArticles { get; set; } = [];
}

public class NewsItem
{
    public string Headline { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime PublishedAt { get; set; }
    public decimal? Sentiment { get; set; }
}
