using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CTrader.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsArticles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Headline = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentimentScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Symbols = table.Column<string>(type: "TEXT", nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    DataType = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    AvgCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    StopLoss = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TakeProfit = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Strategy = table.Column<string>(type: "TEXT", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegimeAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Regime = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    RecommendedStrategy = table.Column<string>(type: "TEXT", nullable: true),
                    Reasoning = table.Column<string>(type: "TEXT", nullable: true),
                    RiskLevel = table.Column<string>(type: "TEXT", nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegimeAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    Side = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Commission = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Strategy = table.Column<string>(type: "TEXT", nullable: true),
                    Regime = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Parameters",
                columns: new[] { "Id", "Category", "DataType", "Description", "Key", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { 1, "Trading", "decimal", "Maximum position size in USD", "MaxPositionSize", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3724), "10000" },
                    { 2, "Trading", "int", "Maximum number of open positions", "MaxOpenPositions", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3730), "5" },
                    { 3, "Trading", "bool", "Enable/disable trading", "TradingEnabled", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3731), "false" },
                    { 4, "Risk", "decimal", "Maximum portfolio risk (2%)", "MaxPortfolioRisk", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3733), "0.02" },
                    { 5, "Risk", "decimal", "Default stop loss percentage (5%)", "StopLossPercent", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3734), "0.05" },
                    { 6, "Risk", "decimal", "Default take profit percentage (10%)", "TakeProfitPercent", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3737), "0.10" },
                    { 7, "Strategy", "json", "Preferred market regimes for trading", "PreferredRegimes", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3738), "[\"RiskOn\",\"Neutral\"]" },
                    { 8, "Strategy", "int", "Analysis interval in minutes", "AnalysisInterval", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3739), "60" },
                    { 9, "LLM", "string", "LLM provider (Anthropic/OpenAI)", "Provider", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3740), "Anthropic" },
                    { 10, "LLM", "string", "LLM model to use", "Model", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3742), "claude-sonnet-4-20250514" },
                    { 11, "LLM", "int", "Maximum tokens for LLM response", "MaxTokens", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3743), "4096" },
                    { 12, "News", "int", "News fetch interval in minutes", "FetchIntervalMinutes", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3744), "15" },
                    { 13, "News", "int", "Maximum articles per fetch", "MaxArticlesPerFetch", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3745), "50" },
                    { 14, "News", "json", "Enabled news sources", "EnabledSources", new DateTime(2026, 6, 7, 20, 21, 48, 803, DateTimeKind.Utc).AddTicks(3747), "[\"Finnhub\",\"AlphaVantage\",\"RSS\"]" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Category",
                table: "ActivityLogs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Level",
                table: "ActivityLogs",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Timestamp",
                table: "ActivityLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Parameters_Category_Key",
                table: "Parameters",
                columns: new[] { "Category", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Symbol",
                table: "Positions",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "NewsArticles");

            migrationBuilder.DropTable(
                name: "Parameters");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "RegimeAnalyses");

            migrationBuilder.DropTable(
                name: "Trades");
        }
    }
}
