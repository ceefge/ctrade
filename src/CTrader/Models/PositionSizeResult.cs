namespace CTrader.Models;

public class PositionSizeResult
{
    public string Symbol { get; set; } = string.Empty;
    public int RecommendedQuantity { get; set; }
    public decimal PositionValue { get; set; }
    public decimal RiskAmount { get; set; }
    public decimal RiskPercent { get; set; }
    public decimal StopLossPrice { get; set; }
    public decimal TakeProfitPrice { get; set; }
    public TradeCosts EstimatedCosts { get; set; } = new();
    public string? Warning { get; set; }
}
