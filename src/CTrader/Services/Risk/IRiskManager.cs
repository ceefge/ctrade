using CTrader.Models;

namespace CTrader.Services.Risk;

public interface IRiskManager
{
    Task<PositionSizeResult> CalculatePositionSizeAsync(string symbol, decimal currentPrice, decimal accountValue);
    Task<(bool ShouldExit, string? Reason)> ShouldExitPositionAsync(string symbol, decimal currentPrice, RegimeAnalysisResult regime);
    Task<decimal> CalculatePortfolioRiskAsync();
}
