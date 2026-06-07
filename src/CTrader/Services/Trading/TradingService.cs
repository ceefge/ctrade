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
    private readonly IBrokerConnector _broker;
    private readonly IServiceScopeFactory _scopeFactory;

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

    // TradingService is a singleton, so scoped services (IActivityLogger, INewsAggregator,
    // IMarketAnalyzer, IRiskManager, IParameterService) must not be captured in the constructor.
    // A fresh scope is created per unit of work via IServiceScopeFactory instead.
    public TradingService(
        ILogger<TradingService> logger,
        IBrokerConnector broker,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _broker = broker;
        _scopeFactory = scopeFactory;

        _broker.ErrorOccurred += (_, error) =>
        {
            _lastError = error;
            ErrorOccurred?.Invoke(this, error);
        };
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Trading Service starting");
        _isRunning = true;
        StatusChanged?.Invoke(this, true);

        using var scope = _scopeFactory.CreateScope();
        var activityLogger = scope.ServiceProvider.GetRequiredService<IActivityLogger>();
        await activityLogger.LogInfoAsync("System", "Trading Service gestartet", source: "TradingService");

        // Try connecting with retries - IB Gateway may not be ready yet
        var maxRetries = 36; // ~3 minutes of retries (2FA login can take time)
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _broker.ConnectAsync(cancellationToken);
                await activityLogger.LogSuccessAsync("System", "Broker verbunden", source: "TradingService");
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
                await activityLogger.LogErrorAsync("System", $"Broker-Verbindung fehlgeschlagen: {ex.Message}", source: "TradingService");
            }
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Trading Service stopping");
        _isRunning = false;
        StatusChanged?.Invoke(this, false);

        using (var scope = _scopeFactory.CreateScope())
        {
            var activityLogger = scope.ServiceProvider.GetRequiredService<IActivityLogger>();
            await activityLogger.LogInfoAsync("System", "Trading Service gestoppt", source: "TradingService");
        }

        await _broker.DisconnectAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var parameters = scope.ServiceProvider.GetRequiredService<IParameterService>();

                var tradingEnabled = await parameters.GetValueAsync("Trading", "TradingEnabled", false);
                if (!tradingEnabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                await RunTradingCycleAsync(scope.ServiceProvider, stoppingToken);

                var intervalMinutes = await parameters.GetValueAsync("Strategy", "AnalysisInterval", 60);
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

    private async Task RunTradingCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var activityLogger = services.GetRequiredService<IActivityLogger>();
        var newsAggregator = services.GetRequiredService<INewsAggregator>();
        var parameters = services.GetRequiredService<IParameterService>();
        var riskManager = services.GetRequiredService<IRiskManager>();

        _logger.LogInformation("Starting trading cycle");
        await activityLogger.LogInfoAsync("Trading", "Trading-Zyklus gestartet", source: "TradingService");

        // 1. Fetch latest news
        var news = await newsAggregator.FetchLatestNewsAsync(cancellationToken);
        await activityLogger.LogInfoAsync("News", $"{news.Count()} News-Artikel abgerufen", source: "NewsAggregator");

        // 2. Analyze market regime
        var regime = await RunAnalysisCoreAsync(services, cancellationToken);
        await activityLogger.LogInfoAsync("Analysis", $"Marktregime: {regime.Regime} (Konfidenz: {regime.Confidence * 100:F0}%)", source: "MarketAnalyzer");

        // 3. Check if regime allows trading
        var preferredRegimes = await parameters.GetValueAsync<List<string>>("Strategy", "PreferredRegimes");
        if (preferredRegimes != null && !preferredRegimes.Contains(regime.Regime.ToString()))
        {
            _logger.LogInformation("Current regime {Regime} not in preferred regimes, skipping trades", regime.Regime);
            await activityLogger.LogWarningAsync("Trading", $"Regime {regime.Regime} nicht in bevorzugten Regimes - keine Trades", source: "TradingService");
            return;
        }

        // 4. Get current positions and check for exit signals
        if (_broker.IsConnected)
        {
            var positions = await _broker.GetPositionsAsync();
            foreach (var position in positions)
            {
                await CheckPositionExitAsync(riskManager, position.Key, position.Value, regime, cancellationToken);
            }
        }

        // 5. Look for entry opportunities (placeholder for actual strategy)
        await activityLogger.LogSuccessAsync("Trading", "Trading-Zyklus abgeschlossen", source: "TradingService");
    }

    public async Task<RegimeAnalysisResult> RunAnalysisAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        return await RunAnalysisCoreAsync(scope.ServiceProvider, cancellationToken);
    }

    private async Task<RegimeAnalysisResult> RunAnalysisCoreAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var newsAggregator = services.GetRequiredService<INewsAggregator>();
        var marketAnalyzer = services.GetRequiredService<IMarketAnalyzer>();

        var news = await newsAggregator.GetCachedNewsAsync();
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

        _currentRegime = await marketAnalyzer.AnalyzeMarketRegimeAsync(news, vix, cancellationToken);
        _lastAnalysisTime = DateTime.UtcNow;
        RegimeUpdated?.Invoke(this, _currentRegime);

        return _currentRegime;
    }

    private async Task CheckPositionExitAsync(IRiskManager riskManager, string symbol, decimal quantity, RegimeAnalysisResult regime, CancellationToken cancellationToken)
    {
        if (!_broker.IsConnected || quantity == 0)
            return;

        try
        {
            var currentPrice = await _broker.GetCurrentPriceAsync(symbol);
            var exitRecommendation = await riskManager.ShouldExitPositionAsync(symbol, currentPrice, regime);

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
