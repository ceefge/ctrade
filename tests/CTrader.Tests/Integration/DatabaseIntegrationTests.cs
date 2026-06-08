using CTrader.Data;
using CTrader.Data.Entities;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CTrader.Tests.Integration;

// Uses a real SQLite in-memory database (not the EF in-memory provider) so that
// relational constraints - unique indexes, NOT NULL, column types - are actually
// enforced, matching the SQLite database the app uses in production.
public class DatabaseIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public DatabaseIntegrationTests()
    {
        // The in-memory database lives as long as the connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Database_ShouldSeedDefaultParameters()
    {
        // Act
        var parameters = await _context.Parameters.ToListAsync();

        // Assert
        parameters.Should().NotBeEmpty();
        parameters.Should().Contain(p => p.Key == "MaxPositionSize");
        parameters.Should().Contain(p => p.Key == "MaxPortfolioRisk");
        parameters.Should().Contain(p => p.Key == "TradingEnabled");
    }

    [Fact]
    public async Task Trades_ShouldStoreAndRetrieve()
    {
        // Arrange
        var trade = new Trade
        {
            Symbol = "AAPL",
            Side = "BUY",
            Quantity = 100,
            Price = 150.50m,
            Commission = 1.00m,
            Strategy = "Momentum",
            Regime = "RiskOn",
            ExecutedAt = DateTime.UtcNow,
            Notes = "Test trade"
        };

        // Act
        _context.Trades.Add(trade);
        await _context.SaveChangesAsync();
        var retrieved = await _context.Trades.FirstOrDefaultAsync(t => t.Symbol == "AAPL");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Quantity.Should().Be(100);
        retrieved.Price.Should().Be(150.50m);
    }

    [Fact]
    public async Task NewsArticles_ShouldUseStringPrimaryKey()
    {
        // Arrange
        var article = new NewsArticle
        {
            Id = "finnhub_123456",
            Headline = "Test Headline",
            Summary = "Test Summary",
            Source = "Finnhub",
            PublishedAt = DateTime.UtcNow
        };

        // Act
        _context.NewsArticles.Add(article);
        await _context.SaveChangesAsync();
        var retrieved = await _context.NewsArticles.FindAsync("finnhub_123456");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Headline.Should().Be("Test Headline");
    }

    [Fact]
    public async Task RegimeAnalyses_ShouldStoreWithTimestamp()
    {
        // Arrange
        var analysis = new RegimeAnalysis
        {
            Regime = "RiskOn",
            Confidence = 0.85m,
            RecommendedStrategy = "Buy momentum stocks",
            Reasoning = "Strong market indicators",
            RiskLevel = "Low"
        };

        // Act
        _context.RegimeAnalyses.Add(analysis);
        await _context.SaveChangesAsync();
        var retrieved = await _context.RegimeAnalyses.FirstOrDefaultAsync();

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.AnalyzedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Positions_ShouldStoreAndRetrieve()
    {
        // Arrange
        var position = new Position
        {
            Symbol = "AAPL",
            Quantity = 100,
            AvgCost = 150m,
            StopLoss = 142.50m,
            TakeProfit = 165m,
            Strategy = "Momentum",
            OpenedAt = DateTime.UtcNow
        };

        // Act
        _context.Positions.Add(position);
        await _context.SaveChangesAsync();
        var retrieved = await _context.Positions.FirstOrDefaultAsync(p => p.Symbol == "AAPL");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Quantity.Should().Be(100);
        retrieved.AvgCost.Should().Be(150m);
        retrieved.StopLoss.Should().Be(142.50m);
    }

    [Fact]
    public async Task Parameters_ShouldEnforceUniqueCategoryKey()
    {
        // "Trading"/"MaxPositionSize" is already seeded - a duplicate must fail.
        _context.Parameters.Add(new Parameter
        {
            Category = "Trading",
            Key = "MaxPositionSize",
            Value = "999",
            DataType = "decimal"
        });

        Func<Task> act = async () => await _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Positions_ShouldEnforceUniqueSymbol()
    {
        _context.Positions.Add(new Position { Symbol = "AAPL", Quantity = 100, AvgCost = 150m, OpenedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        _context.Positions.Add(new Position { Symbol = "AAPL", Quantity = 50, AvgCost = 140m, OpenedAt = DateTime.UtcNow });

        Func<Task> act = async () => await _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
