using CTrader.Models;

namespace CTrader.Services.News;

public interface INewsAggregator
{
    Task<IEnumerable<MarketNews>> FetchLatestNewsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MarketNews>> GetCachedNewsAsync(int hours = 24);
    Task<IEnumerable<MarketNews>> GetNewsBySymbolAsync(string symbol, int hours = 24);
}
