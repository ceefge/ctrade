namespace CTrader.Models;

public class TradeCosts
{
    public decimal Commission { get; set; }
    public decimal Spread { get; set; }
    public decimal SlippageEstimate { get; set; }
    public decimal TotalCost { get; set; }
    public decimal CostAsPercentOfTrade { get; set; }
    public string? Exchange { get; set; }
    public string? Notes { get; set; }
}
