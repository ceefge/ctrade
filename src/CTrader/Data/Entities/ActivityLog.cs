namespace CTrader.Data.Entities;

public class ActivityLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public required string Level { get; set; }  // Info, Warning, Error, Success
    public required string Category { get; set; }  // Trading, News, Analysis, System, Api
    public required string Message { get; set; }
    public string? Details { get; set; }
    public string? Source { get; set; }
}
