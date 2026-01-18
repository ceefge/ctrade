using CTrader.Data;
using CTrader.Models;
using CTrader.Services.Configuration;
using Microsoft.EntityFrameworkCore;

namespace CTrader.Services.Risk;

public class RiskManager : IRiskManager
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IParameterService _parameters;
    private readonly CostCalculator _costCalculator;
    private readonly ILogger<RiskManager> _logger;

    public RiskManager(
        IDbContextFactory<AppDbContext> contextFactory,
        IParameterService parameters,
        CostCalculator costCalculator,
        ILogger<RiskManager> logger)
    {
        _contextFactory = contextFactory;
        _parameters = parameters;
        _costCalculator = costCalculator;
        _logger = logger;
    }

    public async Task<PositionSizeResult> CalculatePositionSizeAsync(string symbol, decimal currentPrice, decimal accountValue)
    {
        var maxPositionSize = await _parameters.GetValueAsync("Trading", "MaxPositionSize", 10000m);
        var maxPortfolioRisk = await _parameters.GetValueAsync("Risk", "MaxPortfolioRisk", 0.02m);
        var stopLossPercent = await _parameters.GetValueAsync("Risk", "StopLossPercent", 0.05m);
        var takeProfitPercent = await _parameters.GetValueAsync("Risk", "TakeProfitPercent", 0.10m);

        // Calculate maximum risk amount
        var maxRiskAmount = accountValue * maxPortfolioRisk;

        // Calculate position size based on stop loss
        var stopLossPrice = currentPrice * (1 - stopLossPercent);
        var riskPerShare = currentPrice - stopLossPrice;
        var positionSizeByRisk = riskPerShare > 0 ? Math.Floor(maxRiskAmount / riskPerShare) : 0;

        // Calculate position size based on max position value
        var positionSizeByValue = Math.Floor(maxPositionSize / currentPrice);

        // Take the smaller of the two
        var recommendedQuantity = (int)Math.Min(positionSizeByRisk, positionSizeByValue);
        var positionValue = recommendedQuantity * currentPrice;
        var riskAmount = recommendedQuantity * riskPerShare;

        // Calculate costs
        var costs = _costCalculator.CalculateRoundTripCosts(symbol, recommendedQuantity, currentPrice, currentPrice * (1 + takeProfitPercent));

        var result = new PositionSizeResult
        {
            Symbol = symbol,
            RecommendedQuantity = recommendedQuantity,
            PositionValue = Math.Round(positionValue, 2),
            RiskAmount = Math.Round(riskAmount, 2),
            RiskPercent = accountValue > 0 ? Math.Round(riskAmount / accountValue * 100, 2) : 0,
            StopLossPrice = Math.Round(stopLossPrice, 2),
            TakeProfitPrice = Math.Round(currentPrice * (1 + takeProfitPercent), 2),
            EstimatedCosts = costs
        };

        // Warnings
        if (recommendedQuantity < 1)
        {
            result.Warning = "Position size too small based on risk parameters";
        }
        else if (costs.CostAsPercentOfTrade > 2)
        {
            result.Warning = "Trading costs exceed 2% of position value";
        }

        _logger.LogInformation("Position size for {Symbol}: {Qty} shares @ {Price} = {Value}, Risk: {Risk} ({RiskPct}%)",
            symbol, recommendedQuantity, currentPrice, positionValue, riskAmount, result.RiskPercent);

        return result;
    }

    public async Task<(bool ShouldExit, string? Reason)> ShouldExitPositionAsync(string symbol, decimal currentPrice, RegimeAnalysisResult regime)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var position = await context.Positions.FirstOrDefaultAsync(p => p.Symbol == symbol);

        if (position == null)
            return (false, null);

        // Check stop loss
        if (position.StopLoss.HasValue && currentPrice <= position.StopLoss.Value)
        {
            return (true, $"Stop loss triggered at {position.StopLoss:C}");
        }

        // Check take profit
        if (position.TakeProfit.HasValue && currentPrice >= position.TakeProfit.Value)
        {
            return (true, $"Take profit reached at {position.TakeProfit:C}");
        }

        // Regime-based exit
        if (regime.Regime == MarketRegime.Crisis)
        {
            return (true, "Crisis regime detected - defensive exit recommended");
        }

        if (regime.Regime == MarketRegime.RiskOff && regime.Confidence > 0.7m)
        {
            // Calculate current P/L
            var pnlPercent = (currentPrice - position.AvgCost) / position.AvgCost;
            if (pnlPercent < 0)
            {
                return (true, "Risk-off regime with position in loss - exit recommended");
            }
        }

        return (false, null);
    }

    public async Task<decimal> CalculatePortfolioRiskAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var positions = await context.Positions.ToListAsync();

        if (!positions.Any())
            return 0;

        var totalRisk = 0m;
        foreach (var position in positions)
        {
            if (position.StopLoss.HasValue)
            {
                var riskPerShare = position.AvgCost - position.StopLoss.Value;
                totalRisk += riskPerShare * position.Quantity;
            }
            else
            {
                // Assume 5% risk if no stop loss set
                totalRisk += position.AvgCost * position.Quantity * 0.05m;
            }
        }

        return Math.Round(totalRisk, 2);
    }
}
