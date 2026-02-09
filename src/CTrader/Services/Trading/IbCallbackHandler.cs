using IBApi;

namespace CTrader.Services.Trading;

/// <summary>
/// Implements the IB API EWrapper interface by inheriting from DefaultEWrapper.
/// Routes relevant callbacks to IbRequestManager. Most methods remain no-ops.
/// </summary>
public class IbCallbackHandler : DefaultEWrapper
{
    private readonly IbRequestManager _requestManager;
    private readonly ILogger _logger;

    public event Action? ConnectionLost;
    public event Action? ConnectionAcknowledged;
    public event Action<int, int, string>? ApiError;

    public IbCallbackHandler(IbRequestManager requestManager, ILogger logger)
    {
        _requestManager = requestManager;
        _logger = logger;
    }

    // --- Connection ---

    public override void connectAck()
    {
        _logger.LogInformation("IB Gateway: Connection acknowledged");
        ConnectionAcknowledged?.Invoke();
    }

    public override void connectionClosed()
    {
        _logger.LogWarning("IB Gateway: Connection closed");
        ConnectionLost?.Invoke();
    }

    public override void nextValidId(int orderId)
    {
        _logger.LogInformation("IB Gateway: Next valid order ID: {OrderId}", orderId);
        _requestManager.SetNextOrderId(orderId);
    }

    public override void managedAccounts(string accountsList)
    {
        _logger.LogInformation("IB Gateway: Managed accounts: {Accounts}", accountsList);
    }

    // --- Errors ---

    public override void error(Exception e)
    {
        _logger.LogError(e, "IB API exception");
    }

    public override void error(string str)
    {
        _logger.LogError("IB API error: {Message}", str);
    }

    public override void error(int id, int errorCode, string errorMsg)
    {
        // Informational messages (not real errors)
        if (errorCode >= 2103 && errorCode <= 2108)
        {
            _logger.LogInformation("IB info {ErrorCode}: {Message}", errorCode, errorMsg);
            return;
        }

        // Market data farm messages
        if (errorCode == 2158 || errorCode == 2119)
        {
            _logger.LogInformation("IB info {ErrorCode}: {Message}", errorCode, errorMsg);
            return;
        }

        _logger.LogError("IB error {ErrorCode} (reqId={RequestId}): {Message}", errorCode, id, errorMsg);

        // Fail any pending request for this ID
        var ex = new InvalidOperationException($"IB error {errorCode}: {errorMsg}");
        _requestManager.TryFailPriceRequest(id, ex);
        _requestManager.TryFailOrderRequest(id, ex);

        ApiError?.Invoke(id, errorCode, errorMsg);
    }

    // --- Market Data ---

    public override void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
    {
        if (price <= 0) return;

        // LAST price (4) preferred, CLOSE (9) as fallback
        if (field == TickType.LAST || field == TickType.CLOSE)
        {
            _requestManager.TryCompletePriceRequest(tickerId, (decimal)price);
        }
    }

    public override void tickSnapshotEnd(int tickerId)
    {
        _logger.LogDebug("IB: Snapshot end for ticker {TickerId}", tickerId);
    }

    // --- Positions ---

    public override void position(string account, Contract contract, double pos, double avgCost)
    {
        if (pos != 0 && !string.IsNullOrEmpty(contract.Symbol))
        {
            _requestManager.AccumulatePosition(contract.Symbol, (decimal)pos);
        }
    }

    public override void positionEnd()
    {
        _requestManager.CompletePositionRequest();
    }

    // --- Account ---

    public override void accountSummary(int reqId, string account, string tag, string value, string currency)
    {
        if (tag == "NetLiquidation" && decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var netLiq))
        {
            _requestManager.TryCompleteAccountRequest(reqId, netLiq);
        }
    }

    // --- Orders ---

    public override void orderStatus(int orderId, string status, double filled, double remaining,
        double avgFillPrice, int permId, int parentId, double lastFillPrice,
        int clientId, string whyHeld, double mktCapPrice)
    {
        _logger.LogInformation("IB Order {OrderId}: {Status} (filled={Filled}, remaining={Remaining})",
            orderId, status, filled, remaining);

        if (status is "Submitted" or "Filled" or "PreSubmitted")
        {
            _requestManager.TryCompleteOrderRequest(orderId);
        }
    }
}
