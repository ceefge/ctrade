using IBApi;

namespace CTrader.Services.Trading;

/// <summary>
/// Real Interactive Brokers Gateway connector implementing IBrokerConnector.
/// Manages the IB API socket connection, EReader thread, and translates
/// callback-based IB API into async/await for the rest of the application.
/// Registered as singleton. Thread-safe.
/// </summary>
public class IbGatewayConnector : IBrokerConnector, IDisposable
{
    private readonly ILogger<IbGatewayConnector> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly IbRequestManager _requestManager;
    private readonly IbCallbackHandler _callbackHandler;

    private EClientSocket? _clientSocket;
    private EReaderMonitorSignal? _signal;
    private EReader? _reader;
    private Thread? _readerThread;

    private volatile bool _isConnected;
    private volatile bool _stopRequested;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private CancellationTokenSource? _reconnectCts;
    private int _reconnecting; // 0 = idle, 1 = a reconnect loop is running
    private readonly int _requestTimeoutMs;

    public bool IsConnected => _isConnected;
    public event EventHandler<bool>? ConnectionStatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public IbGatewayConnector(ILogger<IbGatewayConnector> logger, IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _requestTimeoutMs = configuration.GetValue("IbGateway:RequestTimeoutMs", 15000);

        _requestManager = new IbRequestManager();
        _callbackHandler = new IbCallbackHandler(_requestManager, logger);

        _callbackHandler.ConnectionLost += OnConnectionLost;
        _callbackHandler.ConnectionAcknowledged += OnConnectionAcknowledged;
        _callbackHandler.ApiError += OnApiError;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected) return;

            // A successful (re)connect attempt clears any prior stop request.
            _stopRequested = false;

            // Port from DB (Configuration page), host from env var (Docker) or DB
            var configHost = _configuration["IbGateway:Host"];
            var clientId = _configuration.GetValue("IbGateway:ClientId", 1);
            var host = configHost ?? "localhost";
            var port = _configuration.GetValue("IbGateway:Port", 4001);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var paramService = scope.ServiceProvider.GetRequiredService<CTrader.Services.Configuration.IParameterService>();
                var dbPort = await paramService.GetValueAsync("ApiKeys", "IbGatewayPort", 0);
                if (dbPort > 0) port = dbPort;
                // Only use DB host if no env var override (Docker sets IbGateway__Host)
                if (string.IsNullOrEmpty(configHost))
                {
                    var dbHost = await paramService.GetValueAsync("ApiKeys", "IbGatewayHost", "");
                    if (!string.IsNullOrEmpty(dbHost)) host = dbHost;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read IB Gateway config from DB, using defaults");
            }

            _logger.LogInformation("Connecting to IB Gateway at {Host}:{Port} (clientId={ClientId})", host, port, clientId);

            _signal = new EReaderMonitorSignal();
            _clientSocket = new EClientSocket(_callbackHandler, _signal);

            _clientSocket.eConnect(host, port, clientId);

            _logger.LogInformation("eConnect returned. IsConnected={IsConnected}, ServerVersion={ServerVersion}",
                _clientSocket.IsConnected(), _clientSocket.ServerVersion);

            if (!_clientSocket.IsConnected())
            {
                throw new InvalidOperationException($"Failed to connect to IB Gateway at {host}:{port} (serverVersion={_clientSocket.ServerVersion})");
            }

            // Start the EReader message pump. Capture locals so that a stale
            // reader thread left over from a previous connection operates on its
            // own socket/signal and exits when that socket closes, instead of
            // racing against fields a reconnect has already reassigned.
            var socket = _clientSocket;
            var signal = _signal;
            var reader = new EReader(socket, signal);
            _reader = reader;
            reader.Start();

            _readerThread = new Thread(() =>
            {
                try
                {
                    while (socket.IsConnected())
                    {
                        signal.waitForSignal();
                        reader.processMsgs();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "IB EReader thread terminated");
                }
            })
            {
                IsBackground = true,
                Name = "IB-EReader"
            };
            _readerThread.Start();

            // Request delayed market data (paper trading may not have real-time)
            _clientSocket.reqMarketDataType(3); // 3 = delayed

            _isConnected = true;
            _logger.LogInformation("Connected to IB Gateway successfully");
            ConnectionStatusChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to IB Gateway");
            _isConnected = false;
            ErrorOccurred?.Invoke(this, ex.Message);
            throw;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        // Signal intent and cancel any running reconnect loop before taking the
        // lock so it stops promptly even if it is mid-attempt.
        _stopRequested = true;
        try { _reconnectCts?.Cancel(); } catch (ObjectDisposedException) { }

        await _connectLock.WaitAsync();
        try
        {
            // Re-assert inside the lock so an in-flight reconnect's ConnectAsync
            // (which clears the flag) cannot revive auto-reconnect afterwards.
            _stopRequested = true;
            _requestManager.CancelAllPending("Disconnecting");

            if (_clientSocket?.IsConnected() == true)
            {
                _clientSocket.eDisconnect();
            }

            _isConnected = false;
            ConnectionStatusChanged?.Invoke(this, false);
            _logger.LogInformation("Disconnected from IB Gateway");
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public Task<decimal> GetCurrentPriceAsync(string symbol)
        => FetchSnapshotPriceAsync(IbContractFactory.CreateUsStock(symbol), $"Price request for {symbol}");

    public Task<decimal> GetVixAsync()
        => FetchSnapshotPriceAsync(IbContractFactory.CreateVixIndex(), "VIX request");

    /// <summary>
    /// Requests a one-time snapshot price using delayed data (type 3). If no data
    /// arrives (e.g. market closed / no live delayed stream), retries once with
    /// delayed-frozen (type 4), which returns the last available value, then
    /// restores the default type.
    /// </summary>
    private async Task<decimal> FetchSnapshotPriceAsync(Contract contract, string description)
    {
        EnsureConnected();
        try
        {
            return await RequestSnapshotAsync(contract, description);
        }
        catch (TimeoutException)
        {
            EnsureConnected();
            _logger.LogInformation("{Description}: delayed snapshot timed out, retrying with delayed-frozen (type 4)", description);
            _clientSocket!.reqMarketDataType(4); // delayed-frozen
            try
            {
                return await RequestSnapshotAsync(contract, description);
            }
            finally
            {
                try { _clientSocket.reqMarketDataType(3); } catch { } // restore delayed default
            }
        }
    }

    private async Task<decimal> RequestSnapshotAsync(Contract contract, string description)
    {
        var requestId = _requestManager.GetNextRequestId();
        var tcs = _requestManager.RegisterPriceRequest(requestId);

        using var cts = new CancellationTokenSource(_requestTimeoutMs);
        using var registration = cts.Token.Register(() => _requestManager.TryFailPriceRequest(
            requestId, new TimeoutException($"{description} timed out")));

        _clientSocket!.reqMktData(requestId, contract, "", true, false, null);

        try
        {
            return await tcs.Task;
        }
        finally
        {
            try { _clientSocket.cancelMktData(requestId); } catch { }
        }
    }

    public async Task<Dictionary<string, decimal>> GetPositionsAsync()
    {
        EnsureConnected();

        var tcs = await _requestManager.RegisterPositionRequestAsync(default);

        using var cts = new CancellationTokenSource(_requestTimeoutMs);
        cts.Token.Register(() => _requestManager.FailPositionRequest(
            new TimeoutException("Position request timed out")));

        _clientSocket!.reqPositions();

        try
        {
            return await tcs.Task;
        }
        finally
        {
            try { _clientSocket.cancelPositions(); } catch { }
        }
    }

    public async Task<decimal> GetAccountValueAsync()
    {
        EnsureConnected();

        var requestId = _requestManager.GetNextRequestId();
        var tcs = _requestManager.RegisterAccountRequest(requestId);

        using var cts = new CancellationTokenSource(_requestTimeoutMs);
        cts.Token.Register(() => _requestManager.TryFailAccountRequest(
            requestId, new TimeoutException("Account summary request timed out")));

        _clientSocket!.reqAccountSummary(requestId, "All", "NetLiquidation");

        try
        {
            return await tcs.Task;
        }
        finally
        {
            try { _clientSocket.cancelAccountSummary(requestId); } catch { }
        }
    }

    public async Task<int> PlaceMarketOrderAsync(string symbol, int quantity, bool isBuy)
    {
        EnsureConnected();

        var orderId = _requestManager.GetNextOrderId();
        var tcs = _requestManager.RegisterOrderRequest(orderId);

        var contract = IbContractFactory.CreateUsStock(symbol);
        var order = new IBApi.Order
        {
            Action = isBuy ? "BUY" : "SELL",
            OrderType = "MKT",
            TotalQuantity = quantity,
            Transmit = true
        };

        using var cts = new CancellationTokenSource(_requestTimeoutMs * 3);
        cts.Token.Register(() => _requestManager.TryFailOrderRequest(
            orderId, new TimeoutException($"Order {orderId} for {symbol} was not acknowledged in time")));

        _logger.LogInformation("Placing market order: {Action} {Qty} {Symbol} (orderId={OrderId})",
            order.Action, quantity, symbol, orderId);

        _clientSocket!.placeOrder(orderId, contract, order);

        await tcs.Task;
        return orderId;
    }

    public async Task<int> PlaceLimitOrderAsync(string symbol, int quantity, decimal price, bool isBuy)
    {
        EnsureConnected();

        var orderId = _requestManager.GetNextOrderId();
        var tcs = _requestManager.RegisterOrderRequest(orderId);

        var contract = IbContractFactory.CreateUsStock(symbol);
        var order = new IBApi.Order
        {
            Action = isBuy ? "BUY" : "SELL",
            OrderType = "LMT",
            TotalQuantity = quantity,
            LmtPrice = (double)price,
            Transmit = true
        };

        using var cts = new CancellationTokenSource(_requestTimeoutMs * 3);
        cts.Token.Register(() => _requestManager.TryFailOrderRequest(
            orderId, new TimeoutException($"Order {orderId} for {symbol} was not acknowledged in time")));

        _logger.LogInformation("Placing limit order: {Action} {Qty} {Symbol} @ {Price} (orderId={OrderId})",
            order.Action, quantity, symbol, price, orderId);

        _clientSocket!.placeOrder(orderId, contract, order);

        await tcs.Task;
        return orderId;
    }

    public Task CancelOrderAsync(int orderId)
    {
        EnsureConnected();

        _logger.LogInformation("Cancelling order {OrderId}", orderId);
        _clientSocket!.cancelOrder(orderId);

        return Task.CompletedTask;
    }

    // --- Private helpers ---

    private void EnsureConnected()
    {
        if (!_isConnected || _clientSocket == null || !_clientSocket.IsConnected())
            throw new InvalidOperationException("Not connected to IB Gateway");
    }

    private void OnConnectionLost()
    {
        _isConnected = false;
        _requestManager.CancelAllPending("Connection lost");
        ConnectionStatusChanged?.Invoke(this, false);
        ErrorOccurred?.Invoke(this, "IB Gateway connection lost");

        // Don't auto-reconnect after a deliberate disconnect/dispose.
        if (_stopRequested) return;

        // Ensure only a single reconnect loop runs at a time, even if multiple
        // connectionClosed callbacks arrive.
        if (Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0)
        {
            _ = ReconnectAsync();
        }
    }

    private void OnConnectionAcknowledged()
    {
        _logger.LogDebug("IB Gateway connection acknowledged");
    }

    private void OnApiError(int id, int errorCode, string errorMsg)
    {
        ErrorOccurred?.Invoke(this, $"IB Error {errorCode}: {errorMsg}");
    }

    private async Task ReconnectAsync()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _reconnectCts, cts);
        try { previous?.Dispose(); } catch (ObjectDisposedException) { }
        var ct = cts.Token;

        try
        {
            var delay = TimeSpan.FromSeconds(5);
            var maxDelay = TimeSpan.FromMinutes(2);

            while (!ct.IsCancellationRequested && !_isConnected && !_stopRequested)
            {
                _logger.LogInformation("Reconnecting to IB Gateway in {Delay}s...", delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, ct);
                    await ConnectAsync(ct);
                    _logger.LogInformation("Reconnected to IB Gateway");
                    return;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reconnection attempt failed");
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
                }
            }
        }
        finally
        {
            // Clear the field only if it still points at our CTS, then dispose ours.
            Interlocked.CompareExchange(ref _reconnectCts, null, cts);
            cts.Dispose();
            Interlocked.Exchange(ref _reconnecting, 0);
        }
    }

    public void Dispose()
    {
        _stopRequested = true;
        try { _reconnectCts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _reconnectCts?.Dispose(); } catch (ObjectDisposedException) { }
        _connectLock.Dispose();

        if (_clientSocket?.IsConnected() == true)
        {
            try { _clientSocket.eDisconnect(); } catch { }
        }
    }
}
