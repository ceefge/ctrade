namespace CTrader.Data.Entities;

public class NewsArticle
{
    public required string Id { get; set; }
    public required string Headline { get; set; }
    public string? Summary { get; set; }
    public required string Source { get; set; }
    public string? Url { get; set; }
    public DateTime PublishedAt { get; set; }
    public decimal? SentimentScore { get; set; }
    public string? Symbols { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
