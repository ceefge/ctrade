namespace CTrader.Services.Trading;

/// <summary>
/// Simulated broker connector for development/testing without a live IB Gateway.
/// </summary>
public class SimulatedBrokerConnector : IBrokerConnector
{
    private readonly ILogger<SimulatedBrokerConnector> _logger;
    private readonly IConfiguration _configuration;
    private bool _isConnected;
    private readonly Dictionary<string, decimal> _mockPositions = new();
    private decimal _mockAccountValue = 100000m;

    public bool IsConnected => _isConnected;
    public event EventHandler<bool>? ConnectionStatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public SimulatedBrokerConnector(ILogger<SimulatedBrokerConnector> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var host = _configuration["IbGateway:Host"] ?? "localhost";
        var port = _configuration.GetValue<int>("IbGateway:Port", 4001);

        _logger.LogInformation("Connecting to IB Gateway at {Host}:{Port}", host, port);
        await Task.Delay(1000, cancellationToken);

        _isConnected = true;
        _logger.LogWarning("IB Gateway: Running in SIMULATION mode. Real trading is not available.");
        ConnectionStatusChanged?.Invoke(this, true);
    }

    public Task DisconnectAsync()
    {
        if (_isConnected)
        {
            _isConnected = false;
            _logger.LogInformation("Disconnected from IB Gateway");
            ConnectionStatusChanged?.Invoke(this, false);
        }
        return Task.CompletedTask;
    }

    public async Task<decimal> GetCurrentPriceAsync(string symbol)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to IB Gateway");

        await Task.Delay(100);
        var basePrice = symbol.GetHashCode() % 1000 + 100;
        var randomVariation = new Random().NextDouble() * 10 - 5;
        return Math.Round((decimal)(basePrice + randomVariation), 2);
    }

    public async Task<decimal> GetVixAsync()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to IB Gateway");

        await Task.Delay(100);
        return Math.Round(15m + (decimal)(new Random().NextDouble() * 10), 2);
    }

    public Task<Dictionary<string, decimal>> GetPositionsAsync()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to IB Gateway");

        return Task.FromResult(new Dictionary<string, decimal>(_mockPositions));
    }

    public Task<decimal> GetAccountValueAsync()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to IB Gateway");

        return Task.FromResult(_mockAccountValue);
    }

    public async Task<int> PlaceMarketOrderAsync(string symbol, int quantity, bool isBuy)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to IB Gateway");

        _logger.LogWarning("SIMULATED: Market order {Action} {Qty} {Symbol}", isBuy ? "BUY" : "SELL", quantity, symbol);
        await Task.Delay(500);

        var orderId = new Random().Next(10000, 99999);

        if (isBuy)
        {
            if (_mockPositions.ContainsKey(symbol))
                _mockPositions[symbol] += quantity;
            else
                _mockPositions[symbol] = quantity;
        }
        else
        {
            if (_mockPositions.ContainsKey(symbol))
            {
                _mockPositions[symbol] -= quantity;
                if (_mockPositions[symbol] <= 0)
                    _mockPositions.Remove(symbol);
            }
        }

        _logger.LogInformation("SIMULATED: Order {OrderId} filled", orderId);
        return orderId;
    }

    public async Task<int> PlaceLimitOrderAsync(string symbol, int quantity, decimal price, bool isBuy)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to IB Gateway");

        _logger.LogWarning("SIMULATED: Limit order {Action} {Qty} {Symbol} @ {Price}", isBuy ? "BUY" : "SELL", quantity, symbol, price);
        await Task.Delay(500);

        var orderId = new Random().Next(10000, 99999);
        _logger.LogInformation("SIMULATED: Limit order {OrderId} placed (pending)", orderId);
        return orderId;
    }

    public Task CancelOrderAsync(int orderId)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to IB Gateway");

        _logger.LogWarning("SIMULATED: Cancel order {OrderId}", orderId);
        return Task.CompletedTask;
    }
}
