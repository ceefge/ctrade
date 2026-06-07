using System.Collections.Concurrent;

namespace CTrader.Services.Trading;

/// <summary>
/// Manages IB API request IDs and correlates them with TaskCompletionSource instances
/// to bridge the callback-based IB API with async/await patterns.
/// Thread-safe for use from both the IB reader thread and caller threads.
/// </summary>
public class IbRequestManager
{
    private int _nextRequestId = 1000;
    private int _nextOrderId;
    private readonly ManualResetEventSlim _orderIdReady = new(false);
    private readonly TimeSpan _orderIdTimeout;

    public IbRequestManager(TimeSpan? orderIdTimeout = null)
    {
        _orderIdTimeout = orderIdTimeout ?? TimeSpan.FromSeconds(10);
    }

    private readonly ConcurrentDictionary<int, TaskCompletionSource<decimal>> _priceRequests = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<decimal>> _accountRequests = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<int>> _orderRequests = new();

    private TaskCompletionSource<Dictionary<string, decimal>>? _positionRequest;
    private readonly ConcurrentDictionary<string, decimal> _positionAccumulator = new();
    private readonly SemaphoreSlim _positionSemaphore = new(1, 1);

    public int GetNextRequestId() => Interlocked.Increment(ref _nextRequestId);

    public void SetNextOrderId(int orderId)
    {
        Interlocked.Exchange(ref _nextOrderId, orderId);
        _orderIdReady.Set();
    }

    public int GetNextOrderId()
    {
        // Never place an order before IB has supplied a valid starting ID via
        // nextValidId - otherwise we would submit order ID 0, which can collide
        // with real orders.
        if (!_orderIdReady.Wait(_orderIdTimeout))
            throw new InvalidOperationException(
                "No valid order ID received from IB Gateway (nextValidId timed out).");
        return Interlocked.Increment(ref _nextOrderId) - 1;
    }

    // --- Price requests ---

    public TaskCompletionSource<decimal> RegisterPriceRequest(int requestId)
    {
        var tcs = new TaskCompletionSource<decimal>(TaskCreationOptions.RunContinuationsAsynchronously);
        _priceRequests[requestId] = tcs;
        return tcs;
    }

    public bool TryCompletePriceRequest(int requestId, decimal price)
    {
        if (_priceRequests.TryRemove(requestId, out var tcs))
            return tcs.TrySetResult(price);
        return false;
    }

    public bool TryFailPriceRequest(int requestId, Exception ex)
    {
        if (_priceRequests.TryRemove(requestId, out var tcs))
            return tcs.TrySetException(ex);
        return false;
    }

    // --- Position requests ---

    public async Task<TaskCompletionSource<Dictionary<string, decimal>>> RegisterPositionRequestAsync(CancellationToken ct)
    {
        await _positionSemaphore.WaitAsync(ct);
        _positionAccumulator.Clear();
        var tcs = new TaskCompletionSource<Dictionary<string, decimal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _positionRequest = tcs;
        return tcs;
    }

    public void AccumulatePosition(string symbol, decimal quantity)
    {
        _positionAccumulator.AddOrUpdate(symbol, quantity, (_, existing) => existing + quantity);
    }

    public void CompletePositionRequest()
    {
        // Atomically take ownership so a concurrent timeout (FailPositionRequest)
        // and positionEnd can never both release the semaphore.
        var tcs = Interlocked.Exchange(ref _positionRequest, null);
        if (tcs != null)
        {
            var result = new Dictionary<string, decimal>(_positionAccumulator);
            _positionAccumulator.Clear();
            _positionSemaphore.Release();
            tcs.TrySetResult(result);
        }
    }

    public void FailPositionRequest(Exception ex)
    {
        var tcs = Interlocked.Exchange(ref _positionRequest, null);
        if (tcs != null)
        {
            _positionAccumulator.Clear();
            _positionSemaphore.Release();
            tcs.TrySetException(ex);
        }
    }

    // --- Account requests ---

    public TaskCompletionSource<decimal> RegisterAccountRequest(int requestId)
    {
        var tcs = new TaskCompletionSource<decimal>(TaskCreationOptions.RunContinuationsAsynchronously);
        _accountRequests[requestId] = tcs;
        return tcs;
    }

    public bool TryCompleteAccountRequest(int requestId, decimal value)
    {
        if (_accountRequests.TryRemove(requestId, out var tcs))
            return tcs.TrySetResult(value);
        return false;
    }

    public bool TryFailAccountRequest(int requestId, Exception ex)
    {
        if (_accountRequests.TryRemove(requestId, out var tcs))
            return tcs.TrySetException(ex);
        return false;
    }

    // --- Order requests ---

    public TaskCompletionSource<int> RegisterOrderRequest(int orderId)
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        _orderRequests[orderId] = tcs;
        return tcs;
    }

    public bool TryCompleteOrderRequest(int orderId)
    {
        if (_orderRequests.TryRemove(orderId, out var tcs))
            return tcs.TrySetResult(orderId);
        return false;
    }

    public bool TryFailOrderRequest(int orderId, Exception ex)
    {
        if (_orderRequests.TryRemove(orderId, out var tcs))
            return tcs.TrySetException(ex);
        return false;
    }

    // --- Cleanup ---

    public void CancelAllPending(string reason)
    {
        var ex = new OperationCanceledException(reason);

        foreach (var kvp in _priceRequests)
        {
            if (_priceRequests.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetCanceled();
        }

        foreach (var kvp in _accountRequests)
        {
            if (_accountRequests.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetCanceled();
        }

        foreach (var kvp in _orderRequests)
        {
            if (_orderRequests.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetCanceled();
        }

        FailPositionRequest(ex);
    }
}
