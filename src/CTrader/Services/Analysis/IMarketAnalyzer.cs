using CTrader.Models;

namespace CTrader.Services.Analysis;

public interface IMarketAnalyzer
{
    Task<RegimeAnalysisResult> AnalyzeMarketRegimeAsync(IEnumerable<MarketNews> news, decimal? vix = null, CancellationToken cancellationToken = default);
    Task<string> GetMarketSummaryAsync(IEnumerable<MarketNews> news, CancellationToken cancellationToken = default);
    Task<StockRankingResult> GenerateStockRankingAsync(IEnumerable<MarketNews> news, int topN = 10, CancellationToken cancellationToken = default);
    Task<StockDetailResult> AnalyzeStockDetailAsync(string symbol, IEnumerable<MarketNews> news, decimal? currentPrice = null, CancellationToken cancellationToken = default);
}
