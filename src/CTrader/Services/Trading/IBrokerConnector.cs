namespace CTrader.Services.Trading;

public interface IBrokerConnector
{
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectionStatusChanged;
    event EventHandler<string>? ErrorOccurred;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();

    Task<decimal> GetCurrentPriceAsync(string symbol);
    Task<decimal> GetVixAsync();
    Task<Dictionary<string, decimal>> GetPositionsAsync();
    Task<decimal> GetAccountValueAsync();

    Task<int> PlaceMarketOrderAsync(string symbol, int quantity, bool isBuy);
    Task<int> PlaceLimitOrderAsync(string symbol, int quantity, decimal price, bool isBuy);
    Task CancelOrderAsync(int orderId);
}
