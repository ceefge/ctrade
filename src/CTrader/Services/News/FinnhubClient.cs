using System.Text.Json;
using CTrader.Models;

namespace CTrader.Services.News;

public class FinnhubClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FinnhubClient> _logger;
    private readonly string? _apiKey;

    public FinnhubClient(HttpClient httpClient, ILogger<FinnhubClient> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Finnhub:ApiKey"];
        _httpClient.BaseAddress = new Uri("https://finnhub.io/api/v1/");
    }

    public async Task<IEnumerable<MarketNews>> GetMarketNewsAsync(string category = "general", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Finnhub API key not configured");
            return [];
        }

        try
        {
            var response = await _httpClient.GetAsync($"news?category={category}&token={_apiKey}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var newsItems = JsonSerializer.Deserialize<List<FinnhubNewsItem>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return newsItems?.Select(item => new MarketNews
            {
                Id = $"finnhub_{item.Id}",
                Headline = item.Headline ?? string.Empty,
                Summary = item.Summary,
                Source = $"Finnhub - {item.Source}",
                Url = item.Url,
                PublishedAt = DateTimeOffset.FromUnixTimeSeconds(item.Datetime).UtcDateTime,
                Symbols = item.Related?.Split(',').Select(s => s.Trim()).ToList() ?? []
            }) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news from Finnhub");
            return [];
        }
    }

    public async Task<IEnumerable<MarketNews>> GetCompanyNewsAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Finnhub API key not configured");
            return [];
        }

        try
        {
            var fromStr = from.ToString("yyyy-MM-dd");
            var toStr = to.ToString("yyyy-MM-dd");
            var response = await _httpClient.GetAsync($"company-news?symbol={symbol}&from={fromStr}&to={toStr}&token={_apiKey}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var newsItems = JsonSerializer.Deserialize<List<FinnhubNewsItem>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return newsItems?.Select(item => new MarketNews
            {
                Id = $"finnhub_{item.Id}",
                Headline = item.Headline ?? string.Empty,
                Summary = item.Summary,
                Source = $"Finnhub - {item.Source}",
                Url = item.Url,
                PublishedAt = DateTimeOffset.FromUnixTimeSeconds(item.Datetime).UtcDateTime,
                Symbols = [symbol]
            }) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching company news from Finnhub for {Symbol}", symbol);
            return [];
        }
    }

    private class FinnhubNewsItem
    {
        public long Id { get; set; }
        public string? Headline { get; set; }
        public string? Summary { get; set; }
        public string? Source { get; set; }
        public string? Url { get; set; }
        public long Datetime { get; set; }
        public string? Related { get; set; }
    }
}
