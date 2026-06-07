using CTrader.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CTrader.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Parameter> Parameters => Set<Parameter>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<RegimeAnalysis> RegimeAnalyses => Set<RegimeAnalysis>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Parameter>(entity =>
        {
            entity.HasIndex(e => new { e.Category, e.Key }).IsUnique();
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.DataType).IsRequired();
        });

        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.ExecutedAt);
            entity.Property(e => e.Price).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Commission).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasIndex(e => e.Symbol).IsUnique();
            entity.Property(e => e.AvgCost).HasColumnType("decimal(18,4)");
            entity.Property(e => e.StopLoss).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TakeProfit).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<NewsArticle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PublishedAt);
            entity.Property(e => e.SentimentScore).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<RegimeAnalysis>(entity =>
        {
            entity.HasIndex(e => e.AnalyzedAt);
            entity.Property(e => e.Confidence).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Level);
        });

        SeedDefaultParameters(modelBuilder);
    }

    private static void SeedDefaultParameters(ModelBuilder modelBuilder)
    {
        // Fixed timestamp so HasData seeding is deterministic - DateTime.UtcNow
        // would change on every scaffold and churn each new migration.
        var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var parameters = new List<Parameter>
        {
            // Trading Parameters
            new() { Id = 1, Category = "Trading", Key = "MaxPositionSize", Value = "10000", DataType = "decimal", Description = "Maximum position size in USD" },
            new() { Id = 2, Category = "Trading", Key = "MaxOpenPositions", Value = "5", DataType = "int", Description = "Maximum number of open positions" },
            new() { Id = 3, Category = "Trading", Key = "TradingEnabled", Value = "false", DataType = "bool", Description = "Enable/disable trading" },

            // Risk Parameters
            new() { Id = 4, Category = "Risk", Key = "MaxPortfolioRisk", Value = "0.02", DataType = "decimal", Description = "Maximum portfolio risk (2%)" },
            new() { Id = 5, Category = "Risk", Key = "StopLossPercent", Value = "0.05", DataType = "decimal", Description = "Default stop loss percentage (5%)" },
            new() { Id = 6, Category = "Risk", Key = "TakeProfitPercent", Value = "0.10", DataType = "decimal", Description = "Default take profit percentage (10%)" },

            // Strategy Parameters
            new() { Id = 7, Category = "Strategy", Key = "PreferredRegimes", Value = "[\"RiskOn\",\"Neutral\"]", DataType = "json", Description = "Preferred market regimes for trading" },
            new() { Id = 8, Category = "Strategy", Key = "AnalysisInterval", Value = "60", DataType = "int", Description = "Analysis interval in minutes" },

            // LLM Parameters
            new() { Id = 9, Category = "LLM", Key = "Provider", Value = "Anthropic", DataType = "string", Description = "LLM provider (Anthropic/OpenAI)" },
            new() { Id = 10, Category = "LLM", Key = "Model", Value = "claude-sonnet-4-20250514", DataType = "string", Description = "LLM model to use" },
            new() { Id = 11, Category = "LLM", Key = "MaxTokens", Value = "4096", DataType = "int", Description = "Maximum tokens for LLM response" },

            // News Parameters
            new() { Id = 12, Category = "News", Key = "FetchIntervalMinutes", Value = "15", DataType = "int", Description = "News fetch interval in minutes" },
            new() { Id = 13, Category = "News", Key = "MaxArticlesPerFetch", Value = "50", DataType = "int", Description = "Maximum articles per fetch" },
            new() { Id = 14, Category = "News", Key = "EnabledSources", Value = "[\"Finnhub\",\"AlphaVantage\",\"RSS\"]", DataType = "json", Description = "Enabled news sources" }
        };

        foreach (var parameter in parameters)
            parameter.UpdatedAt = seededAt;

        modelBuilder.Entity<Parameter>().HasData(parameters);
    }
}
