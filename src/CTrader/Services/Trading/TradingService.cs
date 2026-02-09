using CTrader.Models;
using CTrader.Services.Analysis;
using CTrader.Services.Configuration;
using CTrader.Services.Logging;
using CTrader.Services.News;
using CTrader.Services.Risk;

namespace CTrader.Services.Trading;

public class TradingService : BackgroundService, ITradingService
{
    private readonly ILogger<TradingService> _logger;
    private readonly IActivityLogger _activityLogger;
    private readonly IBrokerConnector _broker;
    private readonly INewsAggregator _newsAggregator;
    private readonly IMarketAnalyzer _marketAnalyzer;
    private readonly IRiskManager _riskManager;
    private readonly IParameterService _parameters;

    private bool _isRunning;
    private RegimeAnalysisResult? _currentRegime;
    private DateTime? _lastAnalysisTime;
    private string? _lastError;

    public bool IsRunning => _isRunning;
    public bool IsBrokerConnected => _broker.IsConnected;
    public RegimeAnalysisResult? CurrentRegime => _currentRegime;
    public DateTime? LastAnalysisTime => _lastAnalysisTime;
    public string? LastError => _lastError;

    public event EventHandler<bool>? StatusChanged;
    public event EventHandler<RegimeAnalysisResult>? RegimeUpdated;
    public event EventHandler<string>? ErrorOccurred;

    public TradingService(
        ILogger<TradingService> logger,
        IActivityLogger activityLogger,
        IBrokerConnector broker,
        INewsAggregator newsAggregator,
        IMarketAnalyzer marketAnalyzer,
        IRiskManager riskManager,
        IParameterService parameters)
    {
        _logger = logger;
        _activityLogger = activityLogger;
        _broker = broker;
        _newsAggregator = newsAggregator;
        _marketAnalyzer = marketAnalyzer;
        _riskManager = riskManager;
        _parameters = parameters;

        _broker.ErrorOccurred += (_, error) =>
        {
            _lastError = error;
            ErrorOccurred?.Invoke(this, error);
        };
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Trading Service starting");
        await _activityLogger.LogInfoAsync("System", "Trading Service gestartet", source: "TradingService");
        _isRunning = true;
        StatusChanged?.Invoke(this, true);

        // Try connecting with retries - IB Gateway may not be ready yet
        var maxRetries = 36; // ~3 minutes of retries (2FA login can take time)
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _broker.ConnectAsync(cancellationToken);
                await _activityLogger.LogSuccessAsync("System", "Broker verbunden", source: "TradingService");
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning("Broker connection attempt {Attempt}/{MaxRetries} failed: {Message}", attempt, maxRetries, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to broker after {MaxRetries} attempts", maxRetries);
                _lastError = ex.Message;
                await _activityLogger.LogErrorAsync("System", $"Broker-Verbindung fehlgeschlagen: {ex.Message}", source: "TradingService");
            }
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Trading Service stopping");
        await _activityLogger.LogInfoAsync("System", "Trading Service gestoppt", source: "TradingService");
        _isRunning = false;
        StatusChanged?.Invoke(this, false);

        await _broker.DisconnectAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tradingEnabled = await _parameters.GetValueAsync("Trading", "TradingEnabled", false);
                if (!tradingEnabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                await RunTradingCycleAsync(stoppingToken);

                var intervalMinutes = await _parameters.GetValueAsync("Strategy", "AnalysisInterval", 60);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in trading cycle");
                _lastError = ex.Message;
                ErrorOccurred?.Invoke(this, ex.Message);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task RunTradingCycleAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting trading cycle");
        await _activityLogger.LogInfoAsync("Trading", "Trading-Zyklus gestartet", source: "TradingService");

        // 1. Fetch latest news
        var news = await _newsAggregator.FetchLatestNewsAsync(cancellationToken);
        await _activityLogger.LogInfoAsync("News", $"{news.Count()} News-Artikel abgerufen", source: "NewsAggregator");

        // 2. Analyze market regime
        var regime = await RunAnalysisAsync(cancellationToken);
        await _activityLogger.LogInfoAsync("Analysis", $"Marktregime: {regime.Regime} (Konfidenz: {regime.Confidence * 100:F0}%)", source: "MarketAnalyzer");

        // 3. Check if regime allows trading
        var preferredRegimes = await _parameters.GetValueAsync<List<string>>("Strategy", "PreferredRegimes");
        if (preferredRegimes != null && !preferredRegimes.Contains(regime.Regime.ToString()))
        {
            _logger.LogInformation("Current regime {Regime} not in preferred regimes, skipping trades", regime.Regime);
            await _activityLogger.LogWarningAsync("Trading", $"Regime {regime.Regime} nicht in bevorzugten Regimes - keine Trades", source: "TradingService");
            return;
        }

        // 4. Get current positions and check for exit signals
        if (_broker.IsConnected)
        {
            var positions = await _broker.GetPositionsAsync();
            foreach (var position in positions)
            {
                await CheckPositionExitAsync(position.Key, position.Value, regime, cancellationToken);
            }
        }

        // 5. Look for entry opportunities (placeholder for actual strategy)
        await _activityLogger.LogSuccessAsync("Trading", "Trading-Zyklus abgeschlossen", source: "TradingService");
    }

    public async Task<RegimeAnalysisResult> RunAnalysisAsync(CancellationToken cancellationToken = default)
    {
        var news = await _newsAggregator.GetCachedNewsAsync();
        decimal? vix = null;

        if (_broker.IsConnected)
        {
            try
            {
                vix = await _broker.GetVixAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch VIX");
            }
        }

        _currentRegime = await _marketAnalyzer.AnalyzeMarketRegimeAsync(news, vix, cancellationToken);
        _lastAnalysisTime = DateTime.UtcNow;
        RegimeUpdated?.Invoke(this, _currentRegime);

        return _currentRegime;
    }

    private async Task CheckPositionExitAsync(string symbol, decimal quantity, RegimeAnalysisResult regime, CancellationToken cancellationToken)
    {
        if (!_broker.IsConnected || quantity == 0)
            return;

        try
        {
            var currentPrice = await _broker.GetCurrentPriceAsync(symbol);
            var exitRecommendation = await _riskManager.ShouldExitPositionAsync(symbol, currentPrice, regime);

            if (exitRecommendation.ShouldExit)
            {
                _logger.LogInformation("Exit signal for {Symbol}: {Reason}", symbol, exitRecommendation.Reason);
                // In production, would place exit order here
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking position exit for {Symbol}", symbol);
        }
    }

    Task ITradingService.StartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);
    Task ITradingService.StopAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken);
}
