using CTrader.Models;

namespace CTrader.Services.Analysis;

public interface IMarketAnalyzer
{
    Task<RegimeAnalysisResult> AnalyzeMarketRegimeAsync(IEnumerable<MarketNews> news, decimal? vix = null, CancellationToken cancellationToken = default);
    Task<string> GetMarketSummaryAsync(IEnumerable<MarketNews> news, CancellationToken cancellationToken = default);
}
