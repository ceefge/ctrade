using CTrader.Services.Risk;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CTrader.Tests.Services;

public class CostCalculatorTests
{
    private readonly CostCalculator _calculator;

    public CostCalculatorTests()
    {
        var logger = Mock.Of<ILogger<CostCalculator>>();
        _calculator = new CostCalculator(logger);
    }

    [Fact]
    public void CalculateCosts_ShouldReturnMinimumCommission_ForSmallTrade()
    {
        // Arrange
        var symbol = "AAPL";
        var quantity = 10;
        var price = 150m;

        // Act
        var costs = _calculator.CalculateCosts(symbol, quantity, price);

        // Assert
        costs.Commission.Should().BeGreaterThanOrEqualTo(1.00m);
        costs.TotalCost.Should().BeGreaterThan(0);
        costs.CostAsPercentOfTrade.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateCosts_ShouldCalculateCommissionPerShare_ForLargeTrade()
    {
        // Arrange
        var symbol = "AAPL";
        var quantity = 1000;
        var price = 150m;

        // Act
        var costs = _calculator.CalculateCosts(symbol, quantity, price);

        // Assert
        costs.Commission.Should().BeGreaterThan(1.00m);
        costs.TotalCost.Should().BeGreaterThan(costs.Commission);
    }

    [Fact]
    public void CalculateCosts_ShouldIncludeSpreadAndSlippage()
    {
        // Arrange
        var symbol = "AAPL";
        var quantity = 100;
        var price = 150m;

        // Act
        var costs = _calculator.CalculateCosts(symbol, quantity, price);

        // Assert
        costs.Spread.Should().BeGreaterThan(0);
        costs.SlippageEstimate.Should().BeGreaterThan(0);
        costs.TotalCost.Should().Be(costs.Commission + costs.Spread + costs.SlippageEstimate);
    }

    [Fact]
    public void CalculateRoundTripCosts_ShouldDoubleCommission()
    {
        // Arrange
        var symbol = "AAPL";
        var quantity = 100;
        var entryPrice = 150m;
        var exitPrice = 165m;

        // Act
        var singleCosts = _calculator.CalculateCosts(symbol, quantity, entryPrice);
        var roundTripCosts = _calculator.CalculateRoundTripCosts(symbol, quantity, entryPrice, exitPrice);

        // Assert
        roundTripCosts.Commission.Should().BeGreaterThanOrEqualTo(singleCosts.Commission * 2 - 0.01m);
        roundTripCosts.Notes.Should().Contain("Round-trip");
    }

    [Theory]
    [InlineData(10, 100, 0.5)] // Small trade
    [InlineData(100, 100, 0.3)] // Medium trade
    [InlineData(1000, 100, 0.2)] // Large trade
    public void CalculateCosts_ShouldScaleCostPercentage_WithTradeSize(int quantity, decimal price, decimal maxExpectedPercent)
    {
        // Arrange
        var symbol = "AAPL";

        // Act
        var costs = _calculator.CalculateCosts(symbol, quantity, price);

        // Assert
        costs.CostAsPercentOfTrade.Should().BeLessThanOrEqualTo(maxExpectedPercent * 100);
    }
}
