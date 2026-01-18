using CTrader.Models;

namespace CTrader.Services.Trading;

public interface ITradingService
{
    bool IsRunning { get; }
    bool IsBrokerConnected { get; }
    RegimeAnalysisResult? CurrentRegime { get; }
    DateTime? LastAnalysisTime { get; }
    string? LastError { get; }

    event EventHandler<bool>? StatusChanged;
    event EventHandler<RegimeAnalysisResult>? RegimeUpdated;
    event EventHandler<string>? ErrorOccurred;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<RegimeAnalysisResult> RunAnalysisAsync(CancellationToken cancellationToken = default);
}
