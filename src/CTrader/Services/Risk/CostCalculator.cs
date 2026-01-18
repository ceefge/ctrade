using CTrader.Models;

namespace CTrader.Services.Risk;

public class CostCalculator
{
    private readonly ILogger<CostCalculator> _logger;

    // IB Commission rates (simplified)
    private const decimal IbCommissionPerShare = 0.005m;
    private const decimal IbMinCommission = 1.00m;
    private const decimal IbMaxCommissionPercent = 0.01m;

    // Estimated slippage
    private const decimal EstimatedSlippagePercent = 0.001m;

    public CostCalculator(ILogger<CostCalculator> logger)
    {
        _logger = logger;
    }

    public TradeCosts CalculateCosts(string symbol, int quantity, decimal price, string exchange = "SMART")
    {
        var tradeValue = quantity * price;

        // Commission calculation (IB tiered pricing)
        var commission = Math.Max(IbMinCommission, quantity * IbCommissionPerShare);
        commission = Math.Min(commission, tradeValue * IbMaxCommissionPercent);

        // Spread estimate (varies by liquidity)
        var spreadEstimate = EstimateSpread(symbol, price, tradeValue);

        // Slippage estimate
        var slippageEstimate = tradeValue * EstimatedSlippagePercent;

        var totalCost = commission + spreadEstimate + slippageEstimate;
        var costPercent = tradeValue > 0 ? totalCost / tradeValue : 0;

        var costs = new TradeCosts
        {
            Commission = Math.Round(commission, 2),
            Spread = Math.Round(spreadEstimate, 2),
            SlippageEstimate = Math.Round(slippageEstimate, 2),
            TotalCost = Math.Round(totalCost, 2),
            CostAsPercentOfTrade = Math.Round(costPercent * 100, 4),
            Exchange = exchange,
            Notes = GenerateCostNotes(tradeValue, costPercent)
        };

        _logger.LogDebug("Calculated costs for {Qty} {Symbol}: Commission={Commission}, Spread={Spread}, Slippage={Slippage}, Total={Total} ({Percent}%)",
            quantity, symbol, costs.Commission, costs.Spread, costs.SlippageEstimate, costs.TotalCost, costs.CostAsPercentOfTrade);

        return costs;
    }

    public TradeCosts CalculateRoundTripCosts(string symbol, int quantity, decimal entryPrice, decimal exitPrice)
    {
        var entryCosts = CalculateCosts(symbol, quantity, entryPrice);
        var exitCosts = CalculateCosts(symbol, quantity, exitPrice);

        return new TradeCosts
        {
            Commission = entryCosts.Commission + exitCosts.Commission,
            Spread = entryCosts.Spread + exitCosts.Spread,
            SlippageEstimate = entryCosts.SlippageEstimate + exitCosts.SlippageEstimate,
            TotalCost = entryCosts.TotalCost + exitCosts.TotalCost,
            CostAsPercentOfTrade = entryCosts.CostAsPercentOfTrade + exitCosts.CostAsPercentOfTrade,
            Exchange = entryCosts.Exchange,
            Notes = "Round-trip costs (entry + exit)"
        };
    }

    private static decimal EstimateSpread(string symbol, decimal price, decimal tradeValue)
    {
        // Large-cap stocks typically have tighter spreads
        decimal spreadBps = tradeValue switch
        {
            > 100000 => 5,   // 0.05% for large trades
            > 10000 => 3,    // 0.03% for medium trades
            _ => 2           // 0.02% for small trades
        };

        // Price-based adjustment (penny stocks have wider spreads)
        if (price < 5) spreadBps *= 2;
        else if (price < 20) spreadBps *= 1.5m;

        return tradeValue * (spreadBps / 10000m);
    }

    private static string GenerateCostNotes(decimal tradeValue, decimal costPercent)
    {
        if (costPercent > 0.02m)
            return "High cost ratio - consider larger position size";
        if (tradeValue < 1000)
            return "Small trade - costs may impact returns significantly";
        return "Costs within normal range";
    }
}
