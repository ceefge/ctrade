using System.ServiceModel.Syndication;
using System.Xml;
using CTrader.Models;

namespace CTrader.Services.News;

public class RssFeedClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RssFeedClient> _logger;

    private static readonly List<string> DefaultFeeds =
    [
        "https://feeds.finance.yahoo.com/rss/2.0/headline?s=^GSPC&region=US&lang=en-US",
        "https://www.cnbc.com/id/100003114/device/rss/rss.html",
        "https://feeds.marketwatch.com/marketwatch/topstories/"
    ];

    public RssFeedClient(HttpClient httpClient, ILogger<RssFeedClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<MarketNews>> FetchFeedsAsync(IEnumerable<string>? feedUrls = null, CancellationToken cancellationToken = default)
    {
        var urls = feedUrls ?? DefaultFeeds;
        var allNews = new List<MarketNews>();

        foreach (var url in urls)
        {
            try
            {
                var news = await FetchSingleFeedAsync(url, cancellationToken);
                allNews.AddRange(news);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching RSS feed from {Url}", url);
            }
        }

        return allNews;
    }

    private async Task<IEnumerable<MarketNews>> FetchSingleFeedAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = XmlReader.Create(stream);
            var feed = SyndicationFeed.Load(reader);

            var sourceName = ExtractSourceName(url, feed.Title?.Text);

            return feed.Items.Select(item => new MarketNews
            {
                Id = $"rss_{sourceName}_{item.Id ?? item.Title?.Text?.GetHashCode().ToString() ?? Guid.NewGuid().ToString()}",
                Headline = item.Title?.Text ?? string.Empty,
                Summary = item.Summary?.Text,
                Source = $"RSS - {sourceName}",
                Url = item.Links.FirstOrDefault()?.Uri?.ToString(),
                PublishedAt = item.PublishDate.UtcDateTime,
                Symbols = ExtractSymbols(item.Title?.Text, item.Summary?.Text)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing RSS feed from {Url}", url);
            return [];
        }
    }

    private static string ExtractSourceName(string url, string? title)
    {
        if (!string.IsNullOrEmpty(title))
            return title;

        var uri = new Uri(url);
        return uri.Host.Replace("www.", "").Replace("feeds.", "");
    }

    private static List<string> ExtractSymbols(string? headline, string? summary)
    {
        var symbols = new List<string>();
        var text = $"{headline} {summary}";

        // Common stock symbols pattern ($XXX or stock tickers in caps)
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\$([A-Z]{1,5})\b|\b([A-Z]{2,5})\b(?=\s+(?:stock|shares|price|rises|falls|jumps|drops))");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var symbol = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!string.IsNullOrEmpty(symbol) && !symbols.Contains(symbol))
                symbols.Add(symbol);
        }

        return symbols;
    }
}
