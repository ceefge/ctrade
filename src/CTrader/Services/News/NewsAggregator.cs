using System.Text.Json;
using CTrader.Data;
using CTrader.Data.Entities;
using CTrader.Models;
using CTrader.Services.Configuration;
using CTrader.Services.Logging;
using Microsoft.EntityFrameworkCore;

namespace CTrader.Services.News;

public class NewsAggregator : INewsAggregator
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly FinnhubClient _finnhubClient;
    private readonly AlphaVantageClient _alphaVantageClient;
    private readonly RssFeedClient _rssFeedClient;
    private readonly IParameterService _parameters;
    private readonly IActivityLogger _activityLogger;
    private readonly ILogger<NewsAggregator> _logger;

    public NewsAggregator(
        IDbContextFactory<AppDbContext> contextFactory,
        FinnhubClient finnhubClient,
        AlphaVantageClient alphaVantageClient,
        RssFeedClient rssFeedClient,
        IParameterService parameters,
        IActivityLogger activityLogger,
        ILogger<NewsAggregator> logger)
    {
        _contextFactory = contextFactory;
        _finnhubClient = finnhubClient;
        _alphaVantageClient = alphaVantageClient;
        _rssFeedClient = rssFeedClient;
        _parameters = parameters;
        _activityLogger = activityLogger;
        _logger = logger;
    }

    public async Task<IEnumerable<MarketNews>> FetchLatestNewsAsync(CancellationToken cancellationToken = default)
    {
        var enabledSources = await _parameters.GetValueAsync<List<string>>("News", "EnabledSources")
            ?? new List<string> { "Finnhub", "AlphaVantage", "RSS" };

        var allNews = new List<MarketNews>();
        var tasks = new List<Task<IEnumerable<MarketNews>>>();

        if (enabledSources.Contains("Finnhub"))
            tasks.Add(_finnhubClient.GetMarketNewsAsync(cancellationToken: cancellationToken));

        if (enabledSources.Contains("AlphaVantage"))
            tasks.Add(_alphaVantageClient.GetNewsAndSentimentAsync(cancellationToken: cancellationToken));

        if (enabledSources.Contains("RSS"))
            tasks.Add(_rssFeedClient.FetchFeedsAsync(cancellationToken: cancellationToken));

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            allNews.AddRange(result);
        }

        // Deduplicate and limit
        var maxArticles = await _parameters.GetValueAsync("News", "MaxArticlesPerFetch", 50);
        var deduplicated = DeduplicateNews(allNews)
            .OrderByDescending(n => n.PublishedAt)
            .Take(maxArticles)
            .ToList();

        // Cache to database
        await CacheNewsAsync(deduplicated, cancellationToken);

        _logger.LogInformation("Fetched and cached {Count} news articles from {Sources}", deduplicated.Count, string.Join(", ", enabledSources));
        await _activityLogger.LogSuccessAsync("News", $"{deduplicated.Count} News-Artikel abgerufen und gecacht (Quellen: {string.Join(", ", enabledSources)})", source: "NewsAggregator");

        return deduplicated;
    }

    public async Task<IEnumerable<MarketNews>> GetCachedNewsAsync(int hours = 24)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var cutoff = DateTime.UtcNow.AddHours(-hours);

        var articles = await context.NewsArticles
            .Where(a => a.PublishedAt >= cutoff)
            .OrderByDescending(a => a.PublishedAt)
            .ToListAsync();

        return articles.Select(MapToModel);
    }

    public async Task<IEnumerable<MarketNews>> GetNewsBySymbolAsync(string symbol, int hours = 24)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var cutoff = DateTime.UtcNow.AddHours(-hours);

        var articles = await context.NewsArticles
            .Where(a => a.PublishedAt >= cutoff && a.Symbols != null && a.Symbols.Contains(symbol))
            .OrderByDescending(a => a.PublishedAt)
            .ToListAsync();

        return articles.Select(MapToModel);
    }

    private async Task CacheNewsAsync(IEnumerable<MarketNews> news, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        foreach (var item in news)
        {
            var exists = await context.NewsArticles.AnyAsync(a => a.Id == item.Id, cancellationToken);
            if (!exists)
            {
                context.NewsArticles.Add(new NewsArticle
                {
                    Id = item.Id,
                    Headline = item.Headline,
                    Summary = item.Summary,
                    Source = item.Source,
                    Url = item.Url,
                    PublishedAt = item.PublishedAt,
                    SentimentScore = item.SentimentScore,
                    Symbols = item.Symbols.Count > 0 ? JsonSerializer.Serialize(item.Symbols) : null,
                    FetchedAt = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<MarketNews> DeduplicateNews(IEnumerable<MarketNews> news)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<MarketNews>();

        foreach (var item in news)
        {
            // Deduplicate by headline similarity
            var normalizedHeadline = NormalizeHeadline(item.Headline);
            if (!seen.Contains(normalizedHeadline))
            {
                seen.Add(normalizedHeadline);
                result.Add(item);
            }
        }

        return result;
    }

    private static string NormalizeHeadline(string headline)
    {
        return new string(headline
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray());
    }

    private static MarketNews MapToModel(NewsArticle entity)
    {
        return new MarketNews
        {
            Id = entity.Id,
            Headline = entity.Headline,
            Summary = entity.Summary,
            Source = entity.Source,
            Url = entity.Url,
            PublishedAt = entity.PublishedAt,
            SentimentScore = entity.SentimentScore,
            Symbols = string.IsNullOrEmpty(entity.Symbols)
                ? []
                : JsonSerializer.Deserialize<List<string>>(entity.Symbols) ?? []
        };
    }
}
