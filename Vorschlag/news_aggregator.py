"""
News Aggregator Module für AI Trading Bot
==========================================
Aggregiert Nachrichten aus mehreren Quellen für Marktanalyse.

Unterstützte APIs:
- Finnhub (kostenlos, gute Qualität)
- Alpha Vantage News (kostenlos)
- NewsAPI (kostenlos, 100 Anfragen/Tag)
- Benzinga (Premium, optional)
- RSS Feeds (Fallback)
"""

import asyncio
import aiohttp
import feedparser
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from enum import Enum
from typing import Optional
import hashlib
import json
import os
import logging

logger = logging.getLogger(__name__)


class NewsSource(Enum):
    FINNHUB = "finnhub"
    ALPHA_VANTAGE = "alpha_vantage"
    NEWSAPI = "newsapi"
    BENZINGA = "benzinga"
    RSS = "rss"


class NewsSentiment(Enum):
    VERY_BULLISH = 2
    BULLISH = 1
    NEUTRAL = 0
    BEARISH = -1
    VERY_BEARISH = -2


@dataclass
class NewsArticle:
    """Einzelne Nachricht mit Metadaten"""
    id: str
    headline: str
    summary: str
    source: NewsSource
    url: str
    published_at: datetime
    symbols: list[str] = field(default_factory=list)
    sentiment: Optional[NewsSentiment] = None
    sentiment_score: Optional[float] = None  # -1.0 bis +1.0
    relevance_score: Optional[float] = None  # 0.0 bis 1.0
    categories: list[str] = field(default_factory=list)
    
    def __hash__(self):
        return hash(self.id)
    
    def to_dict(self) -> dict:
        return {
            "id": self.id,
            "headline": self.headline,
            "summary": self.summary[:500],
            "source": self.source.value,
            "url": self.url,
            "published_at": self.published_at.isoformat(),
            "symbols": self.symbols,
            "sentiment": self.sentiment.name if self.sentiment else None,
            "sentiment_score": self.sentiment_score,
            "categories": self.categories
        }


@dataclass
class MarketNews:
    """Aggregierte Nachrichten für Marktanalyse"""
    articles: list[NewsArticle]
    fetch_time: datetime
    overall_sentiment: float  # Durchschnitt aller Sentiment-Scores
    trending_symbols: list[str]
    key_themes: list[str]
    
    def get_for_symbol(self, symbol: str) -> list[NewsArticle]:
        """Filtert Nachrichten für ein bestimmtes Symbol"""
        return [a for a in self.articles if symbol.upper() in [s.upper() for s in a.symbols]]
    
    def get_recent(self, hours: int = 24) -> list[NewsArticle]:
        """Filtert Nachrichten der letzten X Stunden"""
        cutoff = datetime.now() - timedelta(hours=hours)
        return [a for a in self.articles if a.published_at > cutoff]


class FinnhubClient:
    """
    Finnhub API Client
    Docs: https://finnhub.io/docs/api
    
    Kostenlos: 60 Anfragen/Minute
    """
    
    BASE_URL = "https://finnhub.io/api/v1"
    
    def __init__(self, api_key: str):
        self.api_key = api_key
        self._session: Optional[aiohttp.ClientSession] = None
    
    async def _get_session(self) -> aiohttp.ClientSession:
        if self._session is None or self._session.closed:
            self._session = aiohttp.ClientSession()
        return self._session
    
    async def close(self):
        if self._session and not self._session.closed:
            await self._session.close()
    
    async def get_market_news(self, category: str = "general") -> list[NewsArticle]:
        """
        Holt allgemeine Marktnachrichten
        Categories: general, forex, crypto, merger
        """
        session = await self._get_session()
        url = f"{self.BASE_URL}/news"
        params = {"category": category, "token": self.api_key}
        
        try:
            async with session.get(url, params=params) as response:
                if response.status == 200:
                    data = await response.json()
                    return self._parse_articles(data)
                else:
                    logger.error(f"Finnhub market news error: {response.status}")
                    return []
        except Exception as e:
            logger.error(f"Finnhub market news exception: {e}")
            return []
    
    async def get_company_news(self, symbol: str, days_back: int = 7) -> list[NewsArticle]:
        """Holt Nachrichten für ein bestimmtes Unternehmen"""
        session = await self._get_session()
        url = f"{self.BASE_URL}/company-news"
        
        from_date = (datetime.now() - timedelta(days=days_back)).strftime("%Y-%m-%d")
        to_date = datetime.now().strftime("%Y-%m-%d")
        
        params = {
            "symbol": symbol,
            "from": from_date,
            "to": to_date,
            "token": self.api_key
        }
        
        try:
            async with session.get(url, params=params) as response:
                if response.status == 200:
                    data = await response.json()
                    articles = self._parse_articles(data)
                    # Symbol hinzufügen
                    for article in articles:
                        if symbol not in article.symbols:
                            article.symbols.append(symbol)
                    return articles
                else:
                    logger.error(f"Finnhub company news error: {response.status}")
                    return []
        except Exception as e:
            logger.error(f"Finnhub company news exception: {e}")
            return []
    
    async def get_sentiment(self, symbol: str) -> Optional[dict]:
        """Holt Social Sentiment für ein Symbol"""
        session = await self._get_session()
        url = f"{self.BASE_URL}/news-sentiment"
        params = {"symbol": symbol, "token": self.api_key}
        
        try:
            async with session.get(url, params=params) as response:
                if response.status == 200:
                    return await response.json()
                return None
        except Exception as e:
            logger.error(f"Finnhub sentiment exception: {e}")
            return None
    
    def _parse_articles(self, data: list) -> list[NewsArticle]:
        articles = []
        for item in data:
            try:
                article_id = hashlib.md5(
                    f"{item.get('headline', '')}{item.get('datetime', '')}".encode()
                ).hexdigest()
                
                published_at = datetime.fromtimestamp(item.get("datetime", 0))
                
                articles.append(NewsArticle(
                    id=article_id,
                    headline=item.get("headline", ""),
                    summary=item.get("summary", ""),
                    source=NewsSource.FINNHUB,
                    url=item.get("url", ""),
                    published_at=published_at,
                    symbols=item.get("related", "").split(",") if item.get("related") else [],
                    categories=[item.get("category", "general")]
                ))
            except Exception as e:
                logger.warning(f"Error parsing Finnhub article: {e}")
        return articles


class AlphaVantageNewsClient:
    """
    Alpha Vantage News API Client
    Docs: https://www.alphavantage.co/documentation/#news-sentiment
    
    Kostenlos: 25 Anfragen/Tag (Premium für mehr)
    """
    
    BASE_URL = "https://www.alphavantage.co/query"
    
    def __init__(self, api_key: str):
        self.api_key = api_key
        self._session: Optional[aiohttp.ClientSession] = None
    
    async def _get_session(self) -> aiohttp.ClientSession:
        if self._session is None or self._session.closed:
            self._session = aiohttp.ClientSession()
        return self._session
    
    async def close(self):
        if self._session and not self._session.closed:
            await self._session.close()
    
    async def get_news_sentiment(self, 
                                  tickers: Optional[list[str]] = None,
                                  topics: Optional[list[str]] = None,
                                  limit: int = 50) -> list[NewsArticle]:
        """
        Holt Nachrichten mit Sentiment-Analyse
        Topics: blockchain, earnings, ipo, mergers_and_acquisitions, 
                financial_markets, economy_fiscal, economy_monetary,
                economy_macro, energy_transportation, finance, 
                life_sciences, manufacturing, real_estate, 
                retail_wholesale, technology
        """
        session = await self._get_session()
        
        params = {
            "function": "NEWS_SENTIMENT",
            "apikey": self.api_key,
            "limit": limit
        }
        
        if tickers:
            params["tickers"] = ",".join(tickers)
        if topics:
            params["topics"] = ",".join(topics)
        
        try:
            async with session.get(self.BASE_URL, params=params) as response:
                if response.status == 200:
                    data = await response.json()
                    return self._parse_articles(data)
                else:
                    logger.error(f"Alpha Vantage error: {response.status}")
                    return []
        except Exception as e:
            logger.error(f"Alpha Vantage exception: {e}")
            return []
    
    def _parse_articles(self, data: dict) -> list[NewsArticle]:
        articles = []
        feed = data.get("feed", [])
        
        for item in feed:
            try:
                article_id = hashlib.md5(
                    f"{item.get('title', '')}{item.get('time_published', '')}".encode()
                ).hexdigest()
                
                # Zeit parsen (Format: 20231215T143000)
                time_str = item.get("time_published", "")
                try:
                    published_at = datetime.strptime(time_str, "%Y%m%dT%H%M%S")
                except:
                    published_at = datetime.now()
                
                # Sentiment extrahieren
                sentiment_score = float(item.get("overall_sentiment_score", 0))
                sentiment = self._score_to_sentiment(sentiment_score)
                
                # Ticker extrahieren
                ticker_data = item.get("ticker_sentiment", [])
                symbols = [t.get("ticker", "") for t in ticker_data if t.get("ticker")]
                
                # Kategorien/Topics
                topics = item.get("topics", [])
                categories = [t.get("topic", "") for t in topics if t.get("topic")]
                
                articles.append(NewsArticle(
                    id=article_id,
                    headline=item.get("title", ""),
                    summary=item.get("summary", ""),
                    source=NewsSource.ALPHA_VANTAGE,
                    url=item.get("url", ""),
                    published_at=published_at,
                    symbols=symbols,
                    sentiment=sentiment,
                    sentiment_score=sentiment_score,
                    relevance_score=float(item.get("overall_sentiment_score", 0)),
                    categories=categories
                ))
            except Exception as e:
                logger.warning(f"Error parsing Alpha Vantage article: {e}")
        
        return articles
    
    def _score_to_sentiment(self, score: float) -> NewsSentiment:
        if score >= 0.35:
            return NewsSentiment.VERY_BULLISH
        elif score >= 0.15:
            return NewsSentiment.BULLISH
        elif score <= -0.35:
            return NewsSentiment.VERY_BEARISH
        elif score <= -0.15:
            return NewsSentiment.BEARISH
        return NewsSentiment.NEUTRAL


class NewsAPIClient:
    """
    NewsAPI.org Client
    Docs: https://newsapi.org/docs
    
    Kostenlos: 100 Anfragen/Tag, nur Headlines
    """
    
    BASE_URL = "https://newsapi.org/v2"
    
    def __init__(self, api_key: str):
        self.api_key = api_key
        self._session: Optional[aiohttp.ClientSession] = None
    
    async def _get_session(self) -> aiohttp.ClientSession:
        if self._session is None or self._session.closed:
            self._session = aiohttp.ClientSession()
        return self._session
    
    async def close(self):
        if self._session and not self._session.closed:
            await self._session.close()
    
    async def get_top_headlines(self, 
                                 category: str = "business",
                                 country: str = "us") -> list[NewsArticle]:
        """Holt Top-Headlines"""
        session = await self._get_session()
        url = f"{self.BASE_URL}/top-headlines"
        
        params = {
            "category": category,
            "country": country,
            "apiKey": self.api_key
        }
        
        try:
            async with session.get(url, params=params) as response:
                if response.status == 200:
                    data = await response.json()
                    return self._parse_articles(data)
                else:
                    logger.error(f"NewsAPI error: {response.status}")
                    return []
        except Exception as e:
            logger.error(f"NewsAPI exception: {e}")
            return []
    
    async def search_news(self, 
                          query: str,
                          language: str = "en",
                          sort_by: str = "publishedAt") -> list[NewsArticle]:
        """Sucht nach Nachrichten"""
        session = await self._get_session()
        url = f"{self.BASE_URL}/everything"
        
        params = {
            "q": query,
            "language": language,
            "sortBy": sort_by,
            "apiKey": self.api_key
        }
        
        try:
            async with session.get(url, params=params) as response:
                if response.status == 200:
                    data = await response.json()
                    return self._parse_articles(data)
                else:
                    logger.error(f"NewsAPI search error: {response.status}")
                    return []
        except Exception as e:
            logger.error(f"NewsAPI search exception: {e}")
            return []
    
    def _parse_articles(self, data: dict) -> list[NewsArticle]:
        articles = []
        
        for item in data.get("articles", []):
            try:
                article_id = hashlib.md5(
                    f"{item.get('title', '')}{item.get('publishedAt', '')}".encode()
                ).hexdigest()
                
                # Zeit parsen
                time_str = item.get("publishedAt", "")
                try:
                    published_at = datetime.fromisoformat(time_str.replace("Z", "+00:00"))
                except:
                    published_at = datetime.now()
                
                articles.append(NewsArticle(
                    id=article_id,
                    headline=item.get("title", ""),
                    summary=item.get("description", "") or "",
                    source=NewsSource.NEWSAPI,
                    url=item.get("url", ""),
                    published_at=published_at,
                    categories=["general"]
                ))
            except Exception as e:
                logger.warning(f"Error parsing NewsAPI article: {e}")
        
        return articles


class RSSFeedClient:
    """
    RSS Feed Client als Fallback
    Keine API-Keys benötigt
    """
    
    # Wichtige Finanz-RSS-Feeds
    DEFAULT_FEEDS = {
        "reuters_business": "https://www.reutersagency.com/feed/?best-topics=business-finance",
        "cnbc": "https://www.cnbc.com/id/100003114/device/rss/rss.html",
        "marketwatch": "http://feeds.marketwatch.com/marketwatch/topstories/",
        "yahoo_finance": "https://finance.yahoo.com/news/rssindex",
        "seeking_alpha": "https://seekingalpha.com/market_currents.xml",
        "bloomberg_markets": "https://feeds.bloomberg.com/markets/news.rss",
    }
    
    def __init__(self, feeds: Optional[dict[str, str]] = None):
        self.feeds = feeds or self.DEFAULT_FEEDS
    
    async def get_all_feeds(self) -> list[NewsArticle]:
        """Holt alle RSS Feeds parallel"""
        tasks = [self._fetch_feed(name, url) for name, url in self.feeds.items()]
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        articles = []
        for result in results:
            if isinstance(result, list):
                articles.extend(result)
            elif isinstance(result, Exception):
                logger.warning(f"RSS feed error: {result}")
        
        return articles
    
    async def _fetch_feed(self, name: str, url: str) -> list[NewsArticle]:
        """Holt einen einzelnen RSS Feed"""
        try:
            # feedparser ist synchron, also in Thread ausführen
            loop = asyncio.get_event_loop()
            feed = await loop.run_in_executor(None, feedparser.parse, url)
            
            articles = []
            for entry in feed.entries[:20]:  # Max 20 pro Feed
                try:
                    article_id = hashlib.md5(
                        f"{entry.get('title', '')}{entry.get('published', '')}".encode()
                    ).hexdigest()
                    
                    # Zeit parsen
                    published = entry.get("published_parsed") or entry.get("updated_parsed")
                    if published:
                        published_at = datetime(*published[:6])
                    else:
                        published_at = datetime.now()
                    
                    articles.append(NewsArticle(
                        id=article_id,
                        headline=entry.get("title", ""),
                        summary=entry.get("summary", "")[:500],
                        source=NewsSource.RSS,
                        url=entry.get("link", ""),
                        published_at=published_at,
                        categories=[name]
                    ))
                except Exception as e:
                    logger.warning(f"Error parsing RSS entry: {e}")
            
            return articles
        except Exception as e:
            logger.error(f"RSS feed {name} error: {e}")
            return []


class NewsAggregator:
    """
    Hauptklasse zur Aggregation aller Nachrichtenquellen
    """
    
    def __init__(self, config: dict):
        """
        Config sollte API-Keys enthalten:
        {
            "finnhub_key": "...",
            "alpha_vantage_key": "...",
            "newsapi_key": "...",
            "benzinga_key": "..." (optional)
        }
        """
        self.config = config
        self._clients = {}
        self._initialize_clients()
        self._cache: dict[str, tuple[datetime, list[NewsArticle]]] = {}
        self._cache_ttl = timedelta(minutes=5)
    
    def _initialize_clients(self):
        """Initialisiert verfügbare API Clients"""
        
        if self.config.get("finnhub_key"):
            self._clients["finnhub"] = FinnhubClient(self.config["finnhub_key"])
            logger.info("Finnhub client initialized")
        
        if self.config.get("alpha_vantage_key"):
            self._clients["alpha_vantage"] = AlphaVantageNewsClient(
                self.config["alpha_vantage_key"]
            )
            logger.info("Alpha Vantage client initialized")
        
        if self.config.get("newsapi_key"):
            self._clients["newsapi"] = NewsAPIClient(self.config["newsapi_key"])
            logger.info("NewsAPI client initialized")
        
        # RSS immer verfügbar
        self._clients["rss"] = RSSFeedClient()
        logger.info("RSS client initialized")
    
    async def close(self):
        """Schließt alle Client-Sessions"""
        for client in self._clients.values():
            if hasattr(client, "close"):
                await client.close()
    
    async def get_market_news(self, 
                               use_cache: bool = True,
                               include_sentiment: bool = True) -> MarketNews:
        """
        Holt aggregierte Marktnachrichten aus allen Quellen
        """
        cache_key = "market_news"
        
        # Cache prüfen
        if use_cache and cache_key in self._cache:
            cached_time, cached_data = self._cache[cache_key]
            if datetime.now() - cached_time < self._cache_ttl:
                logger.debug("Returning cached market news")
                return self._build_market_news(cached_data)
        
        all_articles = []
        tasks = []
        
        # Finnhub
        if "finnhub" in self._clients:
            tasks.append(self._clients["finnhub"].get_market_news("general"))
        
        # Alpha Vantage (mit Sentiment)
        if "alpha_vantage" in self._clients and include_sentiment:
            tasks.append(self._clients["alpha_vantage"].get_news_sentiment(
                topics=["financial_markets", "economy_macro", "technology"]
            ))
        
        # NewsAPI
        if "newsapi" in self._clients:
            tasks.append(self._clients["newsapi"].get_top_headlines("business"))
        
        # RSS Feeds
        if "rss" in self._clients:
            tasks.append(self._clients["rss"].get_all_feeds())
        
        # Parallel ausführen
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        for result in results:
            if isinstance(result, list):
                all_articles.extend(result)
            elif isinstance(result, Exception):
                logger.warning(f"News fetch error: {result}")
        
        # Deduplizieren (nach ähnlichen Headlines)
        unique_articles = self._deduplicate(all_articles)
        
        # Cache aktualisieren
        self._cache[cache_key] = (datetime.now(), unique_articles)
        
        return self._build_market_news(unique_articles)
    
    async def get_symbol_news(self, 
                               symbol: str,
                               days_back: int = 7) -> MarketNews:
        """Holt Nachrichten für ein bestimmtes Symbol"""
        all_articles = []
        tasks = []
        
        # Finnhub Company News
        if "finnhub" in self._clients:
            tasks.append(self._clients["finnhub"].get_company_news(symbol, days_back))
        
        # Alpha Vantage mit Ticker-Filter
        if "alpha_vantage" in self._clients:
            tasks.append(self._clients["alpha_vantage"].get_news_sentiment(
                tickers=[symbol]
            ))
        
        # NewsAPI Suche
        if "newsapi" in self._clients:
            tasks.append(self._clients["newsapi"].search_news(symbol))
        
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        for result in results:
            if isinstance(result, list):
                all_articles.extend(result)
        
        unique_articles = self._deduplicate(all_articles)
        return self._build_market_news(unique_articles)
    
    def _deduplicate(self, articles: list[NewsArticle]) -> list[NewsArticle]:
        """Entfernt Duplikate basierend auf ähnlichen Headlines"""
        seen_headlines = set()
        unique = []
        
        for article in articles:
            # Normalisierte Headline für Vergleich
            normalized = article.headline.lower().strip()[:50]
            
            if normalized not in seen_headlines:
                seen_headlines.add(normalized)
                unique.append(article)
        
        # Nach Zeit sortieren (neueste zuerst)
        unique.sort(key=lambda x: x.published_at, reverse=True)
        
        return unique
    
    def _build_market_news(self, articles: list[NewsArticle]) -> MarketNews:
        """Baut MarketNews Objekt mit Aggregationen"""
        
        # Gesamt-Sentiment berechnen
        sentiment_scores = [a.sentiment_score for a in articles if a.sentiment_score is not None]
        overall_sentiment = sum(sentiment_scores) / len(sentiment_scores) if sentiment_scores else 0.0
        
        # Trending Symbols
        symbol_counts: dict[str, int] = {}
        for article in articles:
            for symbol in article.symbols:
                symbol_counts[symbol] = symbol_counts.get(symbol, 0) + 1
        
        trending_symbols = sorted(symbol_counts.keys(), 
                                   key=lambda x: symbol_counts[x], 
                                   reverse=True)[:10]
        
        # Key Themes aus Kategorien
        category_counts: dict[str, int] = {}
        for article in articles:
            for cat in article.categories:
                category_counts[cat] = category_counts.get(cat, 0) + 1
        
        key_themes = sorted(category_counts.keys(),
                            key=lambda x: category_counts[x],
                            reverse=True)[:5]
        
        return MarketNews(
            articles=articles,
            fetch_time=datetime.now(),
            overall_sentiment=overall_sentiment,
            trending_symbols=trending_symbols,
            key_themes=key_themes
        )
    
    def format_for_llm(self, market_news: MarketNews, max_articles: int = 20) -> str:
        """
        Formatiert Nachrichten für LLM-Analyse
        """
        recent = market_news.get_recent(hours=24)[:max_articles]
        
        output = []
        output.append(f"=== MARKTNACHRICHTEN ({len(recent)} Artikel, letzte 24h) ===\n")
        output.append(f"Gesamt-Sentiment: {market_news.overall_sentiment:+.2f}")
        output.append(f"Trending Symbole: {', '.join(market_news.trending_symbols[:5])}")
        output.append(f"Hauptthemen: {', '.join(market_news.key_themes)}\n")
        output.append("--- EINZELNE NACHRICHTEN ---\n")
        
        for i, article in enumerate(recent, 1):
            sentiment_str = ""
            if article.sentiment:
                sentiment_str = f" [{article.sentiment.name}]"
            
            symbols_str = ""
            if article.symbols:
                symbols_str = f" ({', '.join(article.symbols[:3])})"
            
            output.append(f"{i}. {article.headline}{sentiment_str}{symbols_str}")
            output.append(f"   {article.summary[:200]}...")
            output.append(f"   Quelle: {article.source.value} | {article.published_at.strftime('%Y-%m-%d %H:%M')}")
            output.append("")
        
        return "\n".join(output)


# Convenience-Funktion für einfache Nutzung
async def fetch_market_news(config: dict) -> MarketNews:
    """Einfache Funktion zum Abrufen von Marktnachrichten"""
    aggregator = NewsAggregator(config)
    try:
        return await aggregator.get_market_news()
    finally:
        await aggregator.close()


# Test/Demo
if __name__ == "__main__":
    async def demo():
        config = {
            "finnhub_key": os.getenv("FINNHUB_API_KEY"),
            "alpha_vantage_key": os.getenv("ALPHA_VANTAGE_API_KEY"),
            "newsapi_key": os.getenv("NEWSAPI_KEY"),
        }
        
        aggregator = NewsAggregator(config)
        
        try:
            print("Fetching market news...")
            news = await aggregator.get_market_news()
            
            print(f"\nGefunden: {len(news.articles)} Artikel")
            print(f"Gesamt-Sentiment: {news.overall_sentiment:+.2f}")
            print(f"Trending: {news.trending_symbols[:5]}")
            print(f"Themes: {news.key_themes}")
            
            print("\n" + "="*50)
            print(aggregator.format_for_llm(news, max_articles=5))
            
        finally:
            await aggregator.close()
    
    asyncio.run(demo())
