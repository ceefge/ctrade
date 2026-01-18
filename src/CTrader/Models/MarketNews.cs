namespace CTrader.Models;

public class MarketNews
{
    public string Id { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime PublishedAt { get; set; }
    public decimal? SentimentScore { get; set; }
    public List<string> Symbols { get; set; } = [];
}
