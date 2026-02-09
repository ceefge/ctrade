using System.Text.Json;
using CTrader.Models;
using CTrader.Services.Configuration;

namespace CTrader.Services.News;

public class AlphaVantageClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AlphaVantageClient> _logger;
    private readonly IParameterService _parameters;
    private readonly string? _configApiKey;

    public AlphaVantageClient(HttpClient httpClient, ILogger<AlphaVantageClient> logger, IConfiguration configuration, IParameterService parameters)
    {
        _httpClient = httpClient;
        _logger = logger;
        _parameters = parameters;
        _configApiKey = configuration["AlphaVantage:ApiKey"];
        _httpClient.BaseAddress = new Uri("https://www.alphavantage.co/");
    }

    private async Task<string?> GetApiKeyAsync()
    {
        var dbKey = await _parameters.GetValueAsync("ApiKeys", "AlphaVantage", "");
        return !string.IsNullOrEmpty(dbKey) ? dbKey : _configApiKey;
    }

    public async Task<IEnumerable<MarketNews>> GetNewsAndSentimentAsync(string? tickers = null, string? topics = null, CancellationToken cancellationToken = default)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Alpha Vantage API key not configured");
            return [];
        }

        try
        {
            var url = $"query?function=NEWS_SENTIMENT&apikey={apiKey}";
            if (!string.IsNullOrEmpty(tickers))
                url += $"&tickers={tickers}";
            if (!string.IsNullOrEmpty(topics))
                url += $"&topics={topics}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<AlphaVantageNewsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Feed?.Select(item => new MarketNews
            {
                Id = $"av_{item.TimePublished}_{item.Title?.GetHashCode()}",
                Headline = item.Title ?? string.Empty,
                Summary = item.Summary,
                Source = $"AlphaVantage - {item.Source}",
                Url = item.Url,
                PublishedAt = ParseAlphaVantageDate(item.TimePublished),
                SentimentScore = item.OverallSentimentScore,
                Symbols = item.TickerSentiment?.Select(t => t.Ticker ?? string.Empty).ToList() ?? []
            }) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news from Alpha Vantage");
            return [];
        }
    }

    private static DateTime ParseAlphaVantageDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
            return DateTime.UtcNow;

        // Format: 20240115T120000
        if (DateTime.TryParseExact(dateStr, "yyyyMMddTHHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var date))
            return date.ToUniversalTime();

        return DateTime.UtcNow;
    }

    private class AlphaVantageNewsResponse
    {
        public List<AlphaVantageNewsItem>? Feed { get; set; }
    }

    private class AlphaVantageNewsItem
    {
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string? Source { get; set; }
        public string? Url { get; set; }
        public string? TimePublished { get; set; }
        public decimal? OverallSentimentScore { get; set; }
        public List<TickerSentiment>? TickerSentiment { get; set; }
    }

    private class TickerSentiment
    {
        public string? Ticker { get; set; }
    }
}
