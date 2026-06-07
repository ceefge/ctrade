using CTrader.Services.Trading;
using FluentAssertions;
using Xunit;

namespace CTrader.Tests.Services;

/// <summary>
/// Regression tests for the IB request/response bridge (IbRequestManager).
/// These guard the order-ID, order/account timeout, and position-request
/// concurrency bugs fixed in the trading subsystem review (#5).
/// </summary>
public class IbRequestManagerTests
{
    // --- Order IDs ---

    [Fact]
    public void GetNextOrderId_Throws_WhenNoValidIdReceived()
    {
        // nextValidId never arrived: must throw rather than place order ID 0.
        var manager = new IbRequestManager(TimeSpan.FromMilliseconds(50));

        Action act = () => manager.GetNextOrderId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetNextOrderId_ReturnsSequentialIds_StartingFromNextValidId()
    {
        var manager = new IbRequestManager();
        manager.SetNextOrderId(5);

        manager.GetNextOrderId().Should().Be(5);
        manager.GetNextOrderId().Should().Be(6);
        manager.GetNextOrderId().Should().Be(7);
    }

    [Fact]
    public void GetNextOrderId_DoesNotThrow_OnceValidIdReceived()
    {
        var manager = new IbRequestManager(TimeSpan.FromMilliseconds(50));
        manager.SetNextOrderId(10);

        Action act = () => manager.GetNextOrderId();

        act.Should().NotThrow();
    }

    // --- Order requests ---

    [Fact]
    public async Task TryFailOrderRequest_FailsPendingOrder_WithException()
    {
        var manager = new IbRequestManager();
        manager.SetNextOrderId(1);
        var orderId = manager.GetNextOrderId();
        var tcs = manager.RegisterOrderRequest(orderId);

        var failed = manager.TryFailOrderRequest(orderId, new TimeoutException("not acknowledged"));

        failed.Should().BeTrue();
        Func<Task> awaiting = async () => await tcs.Task;
        await awaiting.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public void TryFailOrderRequest_ReturnsFalse_WhenOrderAlreadyCompleted()
    {
        var manager = new IbRequestManager();
        manager.SetNextOrderId(1);
        var orderId = manager.GetNextOrderId();
        manager.RegisterOrderRequest(orderId);

        manager.TryCompleteOrderRequest(orderId).Should().BeTrue();
        // A racing timeout after completion must be a no-op.
        manager.TryFailOrderRequest(orderId, new TimeoutException()).Should().BeFalse();
    }

    // --- Account requests ---

    [Fact]
    public async Task TryFailAccountRequest_FailsPendingRequest_RatherThanReturningZero()
    {
        var manager = new IbRequestManager();
        var tcs = manager.RegisterAccountRequest(1);

        var failed = manager.TryFailAccountRequest(1, new TimeoutException("account timed out"));

        failed.Should().BeTrue();
        Func<Task> awaiting = async () => await tcs.Task;
        await awaiting.Should().ThrowAsync<TimeoutException>();
    }

    // --- Position requests ---

    [Fact]
    public async Task PositionRequest_CompletesWithAccumulatedPositions()
    {
        var manager = new IbRequestManager();
        var tcs = await manager.RegisterPositionRequestAsync(CancellationToken.None);

        manager.AccumulatePosition("AAPL", 10m);
        manager.AccumulatePosition("MSFT", 5m);
        manager.AccumulatePosition("AAPL", 3m); // same symbol accumulates
        manager.CompletePositionRequest();

        var result = await tcs.Task;
        result.Should().HaveCount(2);
        result["AAPL"].Should().Be(13m);
        result["MSFT"].Should().Be(5m);
    }

    [Fact]
    public async Task PositionRequest_DoesNotDoubleReleaseSemaphore_OnCompleteThenFail()
    {
        var manager = new IbRequestManager();
        var tcs = await manager.RegisterPositionRequestAsync(CancellationToken.None);
        manager.AccumulatePosition("AAPL", 10m);

        manager.CompletePositionRequest();
        (await tcs.Task).Should().ContainKey("AAPL");

        // A timeout firing after positionEnd already completed the request must
        // be a no-op - otherwise the semaphore is released twice and two
        // position requests could run concurrently.
        manager.FailPositionRequest(new TimeoutException());

        // First registration acquires the single slot...
        await manager.RegisterPositionRequestAsync(CancellationToken.None);

        // ...a second one must block (it would succeed instantly if the
        // semaphore had been double-released).
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        Func<Task> second = async () => await manager.RegisterPositionRequestAsync(cts.Token);
        await second.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FailPositionRequest_PropagatesException_AndReleasesSlot()
    {
        var manager = new IbRequestManager();
        var tcs = await manager.RegisterPositionRequestAsync(CancellationToken.None);

        manager.FailPositionRequest(new TimeoutException("position timed out"));

        Func<Task> awaiting = async () => await tcs.Task;
        await awaiting.Should().ThrowAsync<TimeoutException>();

        // Slot was released, so a new request can be registered.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var next = await manager.RegisterPositionRequestAsync(cts.Token);
        Assert.NotNull(next);
    }

    // --- Request IDs ---

    [Fact]
    public void GetNextRequestId_ReturnsIncreasingValues_StartingAbove1000()
    {
        var manager = new IbRequestManager();

        var first = manager.GetNextRequestId();
        var second = manager.GetNextRequestId();

        first.Should().BeGreaterThan(1000);
        second.Should().Be(first + 1);
    }
}
