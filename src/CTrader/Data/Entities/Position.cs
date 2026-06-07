namespace CTrader.Data.Entities;

public class Position
{
    public int Id { get; set; }
    public required string Symbol { get; set; }
    public int Quantity { get; set; }
    public decimal AvgCost { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit { get; set; }
    public string? Strategy { get; set; }
    public DateTime OpenedAt { get; set; }
}
