using CTrader.Data;
using CTrader.Services.Configuration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CTrader.Tests.Services;

public class ParameterServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ParameterService _parameterService;

    public ParameterServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        var mockFactory = new Mock<IDbContextFactory<AppDbContext>>();
        mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(options));

        var logger = Mock.Of<ILogger<ParameterService>>();
        _parameterService = new ParameterService(mockFactory.Object, logger);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnSeededParameters()
    {
        // Act
        var parameters = await _parameterService.GetAllAsync();

        // Assert
        parameters.Should().NotBeEmpty();
        parameters.Should().Contain(p => p.Category == "Trading");
        parameters.Should().Contain(p => p.Category == "Risk");
    }

    [Fact]
    public async Task GetByCategoryAsync_ShouldReturnOnlyMatchingCategory()
    {
        // Act
        var tradingParams = await _parameterService.GetByCategoryAsync("Trading");

        // Assert
        tradingParams.Should().NotBeEmpty();
        tradingParams.Should().OnlyContain(p => p.Category == "Trading");
    }

    [Fact]
    public async Task GetAsync_ShouldReturnSpecificParameter()
    {
        // Act
        var param = await _parameterService.GetAsync("Trading", "MaxPositionSize");

        // Assert
        param.Should().NotBeNull();
        param!.Value.Should().Be("10000");
        param.DataType.Should().Be("decimal");
    }

    [Fact]
    public async Task GetValueAsync_ShouldParseDecimalValue()
    {
        // Act
        var value = await _parameterService.GetValueAsync("Trading", "MaxPositionSize", 0m);

        // Assert
        value.Should().Be(10000m);
    }

    [Fact]
    public async Task GetValueAsync_ShouldParseIntValue()
    {
        // Act
        var value = await _parameterService.GetValueAsync("Trading", "MaxOpenPositions", 0);

        // Assert
        value.Should().Be(5);
    }

    [Fact]
    public async Task GetValueAsync_ShouldParseBoolValue()
    {
        // Act
        var value = await _parameterService.GetValueAsync("Trading", "TradingEnabled", true);

        // Assert
        value.Should().BeFalse();
    }

    [Fact]
    public async Task GetValueAsync_ShouldParseJsonValue()
    {
        // Act
        var value = await _parameterService.GetValueAsync<List<string>>("Strategy", "PreferredRegimes");

        // Assert
        value.Should().NotBeNull();
        value.Should().Contain("RiskOn");
        value.Should().Contain("Neutral");
    }

    [Fact]
    public async Task SetAsync_ShouldCreateNewParameter()
    {
        // Arrange
        var category = "Test";
        var key = "NewParam";
        var value = "TestValue";

        // Act
        await _parameterService.SetAsync(category, key, value, "string", "Test description");
        var result = await _parameterService.GetAsync(category, key);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be(value);
        result.Description.Should().Be("Test description");
    }

    [Fact]
    public async Task SetAsync_ShouldUpdateExistingParameter()
    {
        // Arrange
        var newValue = "20000";

        // Act
        await _parameterService.SetAsync("Trading", "MaxPositionSize", newValue, "decimal");
        var result = await _parameterService.GetValueAsync("Trading", "MaxPositionSize", 0m);

        // Assert
        result.Should().Be(20000m);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveParameter()
    {
        // Arrange
        await _parameterService.SetAsync("Test", "ToDelete", "value", "string");

        // Act
        await _parameterService.DeleteAsync("Test", "ToDelete");
        var result = await _parameterService.GetAsync("Test", "ToDelete");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCategoriesAsync_ShouldReturnDistinctCategories()
    {
        // Act
        var categories = await _parameterService.GetCategoriesAsync();

        // Assert
        categories.Should().Contain("Trading");
        categories.Should().Contain("Risk");
        categories.Should().Contain("Strategy");
        categories.Should().Contain("LLM");
        categories.Should().Contain("News");
        categories.Should().OnlyHaveUniqueItems();
    }
}
