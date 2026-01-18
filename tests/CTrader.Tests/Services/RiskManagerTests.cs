using CTrader.Data;
using CTrader.Data.Entities;
using CTrader.Models;
using CTrader.Services.Configuration;
using CTrader.Services.Risk;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CTrader.Tests.Services;

public class RiskManagerTests
{
    private readonly Mock<IDbContextFactory<AppDbContext>> _contextFactoryMock;
    private readonly Mock<IParameterService> _parameterServiceMock;
    private readonly CostCalculator _costCalculator;
    private readonly RiskManager _riskManager;

    public RiskManagerTests()
    {
        _contextFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();
        _parameterServiceMock = new Mock<IParameterService>();
        _costCalculator = new CostCalculator(Mock.Of<ILogger<CostCalculator>>());

        var logger = Mock.Of<ILogger<RiskManager>>();
        _riskManager = new RiskManager(_contextFactoryMock.Object, _parameterServiceMock.Object, _costCalculator, logger);
    }

    [Fact]
    public async Task CalculatePositionSizeAsync_ShouldRespectMaxPositionSize()
    {
        // Arrange
        _parameterServiceMock.Setup(p => p.GetValueAsync("Trading", "MaxPositionSize", It.IsAny<decimal>()))
            .ReturnsAsync(10000m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "MaxPortfolioRisk", It.IsAny<decimal>()))
            .ReturnsAsync(0.02m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "StopLossPercent", It.IsAny<decimal>()))
            .ReturnsAsync(0.05m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "TakeProfitPercent", It.IsAny<decimal>()))
            .ReturnsAsync(0.10m);

        // Act
        var result = await _riskManager.CalculatePositionSizeAsync("AAPL", 150m, 100000m);

        // Assert
        result.PositionValue.Should().BeLessThanOrEqualTo(10000m);
        result.RecommendedQuantity.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CalculatePositionSizeAsync_ShouldCalculateStopLoss()
    {
        // Arrange
        var stopLossPercent = 0.05m;
        var currentPrice = 100m;

        _parameterServiceMock.Setup(p => p.GetValueAsync("Trading", "MaxPositionSize", It.IsAny<decimal>()))
            .ReturnsAsync(10000m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "MaxPortfolioRisk", It.IsAny<decimal>()))
            .ReturnsAsync(0.02m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "StopLossPercent", It.IsAny<decimal>()))
            .ReturnsAsync(stopLossPercent);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "TakeProfitPercent", It.IsAny<decimal>()))
            .ReturnsAsync(0.10m);

        // Act
        var result = await _riskManager.CalculatePositionSizeAsync("TEST", currentPrice, 100000m);

        // Assert
        result.StopLossPrice.Should().Be(currentPrice * (1 - stopLossPercent));
    }

    [Fact]
    public async Task CalculatePositionSizeAsync_ShouldCalculateTakeProfit()
    {
        // Arrange
        var takeProfitPercent = 0.10m;
        var currentPrice = 100m;

        _parameterServiceMock.Setup(p => p.GetValueAsync("Trading", "MaxPositionSize", It.IsAny<decimal>()))
            .ReturnsAsync(10000m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "MaxPortfolioRisk", It.IsAny<decimal>()))
            .ReturnsAsync(0.02m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "StopLossPercent", It.IsAny<decimal>()))
            .ReturnsAsync(0.05m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "TakeProfitPercent", It.IsAny<decimal>()))
            .ReturnsAsync(takeProfitPercent);

        // Act
        var result = await _riskManager.CalculatePositionSizeAsync("TEST", currentPrice, 100000m);

        // Assert
        result.TakeProfitPrice.Should().Be(currentPrice * (1 + takeProfitPercent));
    }

    [Fact]
    public async Task CalculatePositionSizeAsync_ShouldIncludeTradeCosts()
    {
        // Arrange
        _parameterServiceMock.Setup(p => p.GetValueAsync("Trading", "MaxPositionSize", It.IsAny<decimal>()))
            .ReturnsAsync(10000m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "MaxPortfolioRisk", It.IsAny<decimal>()))
            .ReturnsAsync(0.02m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "StopLossPercent", It.IsAny<decimal>()))
            .ReturnsAsync(0.05m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "TakeProfitPercent", It.IsAny<decimal>()))
            .ReturnsAsync(0.10m);

        // Act
        var result = await _riskManager.CalculatePositionSizeAsync("AAPL", 150m, 100000m);

        // Assert
        result.EstimatedCosts.Should().NotBeNull();
        result.EstimatedCosts.TotalCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CalculatePositionSizeAsync_ShouldWarn_WhenPositionTooSmall()
    {
        // Arrange
        _parameterServiceMock.Setup(p => p.GetValueAsync("Trading", "MaxPositionSize", It.IsAny<decimal>()))
            .ReturnsAsync(100m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "MaxPortfolioRisk", It.IsAny<decimal>()))
            .ReturnsAsync(0.001m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "StopLossPercent", It.IsAny<decimal>()))
            .ReturnsAsync(0.05m);
        _parameterServiceMock.Setup(p => p.GetValueAsync("Risk", "TakeProfitPercent", It.IsAny<decimal>()))
            .ReturnsAsync(0.10m);

        // Act
        var result = await _riskManager.CalculatePositionSizeAsync("AAPL", 500m, 1000m);

        // Assert
        result.RecommendedQuantity.Should().BeLessThanOrEqualTo(1);
    }
}
