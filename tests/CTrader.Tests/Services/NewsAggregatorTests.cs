using CTrader.Data;
using CTrader.Data.Entities;
using CTrader.Services.Configuration;
using CTrader.Services.Logging;
using CTrader.Services.News;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CTrader.Tests.Services;

/// <summary>
/// Tests for the DB-backed read paths of NewsAggregator. Uses a real SQLite
/// in-memory database so that LINQ string matching is translated to SQL exactly
/// as in production (the EF in-memory provider would run it as LINQ-to-objects).
/// </summary>
public class NewsAggregatorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _factory;
    private readonly NewsAggregator _aggregator;

    public NewsAggregatorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _factory = new TestDbContextFactory(options);
        using (var context = _factory.CreateDbContext())
        {
            context.Database.EnsureCreated();
        }

        // The external clients are not used by the cached/symbol read paths.
        var config = Mock.Of<IConfiguration>();
        var parameters = Mock.Of<IParameterService>();
        var finnhub = new FinnhubClient(new HttpClient(), Mock.Of<ILogger<FinnhubClient>>(), config, parameters);
        var alpha = new AlphaVantageClient(new HttpClient(), Mock.Of<ILogger<AlphaVantageClient>>(), config, parameters);
        var rss = new RssFeedClient(new HttpClient(), Mock.Of<ILogger<RssFeedClient>>());

        _aggregator = new NewsAggregator(
            _factory, finnhub, alpha, rss, parameters,
            Mock.Of<IActivityLogger>(), Mock.Of<ILogger<NewsAggregator>>());
    }

    private void SeedArticle(string id, string headline, DateTime publishedAt, string? symbolsJson)
    {
        using var context = _factory.CreateDbContext();
        context.NewsArticles.Add(new NewsArticle
        {
            Id = id,
            Headline = headline,
            Source = "Test",
            PublishedAt = publishedAt,
            Symbols = symbolsJson,
            FetchedAt = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task GetNewsBySymbolAsync_MatchesWholeToken_NotSubstring()
    {
        var now = DateTime.UtcNow;
        SeedArticle("a1", "Alibaba climbs", now.AddHours(-1), "[\"BABA\"]");
        SeedArticle("a2", "Boeing rises", now.AddHours(-2), "[\"BA\"]");

        var result = (await _aggregator.GetNewsBySymbolAsync("BA")).ToList();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a2");
    }

    [Fact]
    public async Task GetNewsBySymbolAsync_FindsSymbolAmongMultiple()
    {
        SeedArticle("m1", "Mixed coverage", DateTime.UtcNow.AddHours(-1), "[\"AAPL\",\"MSFT\",\"BA\"]");

        var result = (await _aggregator.GetNewsBySymbolAsync("MSFT")).ToList();

        result.Should().ContainSingle().Which.Id.Should().Be("m1");
    }

    [Fact]
    public async Task GetNewsBySymbolAsync_RespectsTimeCutoff()
    {
        SeedArticle("recent", "Recent BA news", DateTime.UtcNow.AddHours(-1), "[\"BA\"]");
        SeedArticle("old", "Old BA news", DateTime.UtcNow.AddHours(-50), "[\"BA\"]");

        var result = (await _aggregator.GetNewsBySymbolAsync("BA", hours: 24)).ToList();

        result.Should().ContainSingle().Which.Id.Should().Be("recent");
    }

    [Fact]
    public async Task GetCachedNewsAsync_ReturnsRecentArticlesNewestFirst()
    {
        SeedArticle("older", "Older", DateTime.UtcNow.AddHours(-5), null);
        SeedArticle("newer", "Newer", DateTime.UtcNow.AddHours(-1), null);

        var result = (await _aggregator.GetCachedNewsAsync(24)).ToList();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("newer");
        result[1].Id.Should().Be("older");
    }

    [Fact]
    public async Task GetCachedNewsAsync_ExcludesArticlesOlderThanCutoff()
    {
        SeedArticle("within", "Within window", DateTime.UtcNow.AddHours(-10), null);
        SeedArticle("outside", "Outside window", DateTime.UtcNow.AddHours(-30), null);

        var result = (await _aggregator.GetCachedNewsAsync(24)).ToList();

        result.Should().ContainSingle().Which.Id.Should().Be("within");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
