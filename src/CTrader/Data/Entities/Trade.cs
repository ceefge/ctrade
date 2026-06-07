namespace CTrader.Data.Entities;

public class Trade
{
    public int Id { get; set; }
    public required string Symbol { get; set; }
    public required string Side { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal? Commission { get; set; }
    public string? Strategy { get; set; }
    public string? Regime { get; set; }
    public DateTime ExecutedAt { get; set; }
    public string? Notes { get; set; }
}
