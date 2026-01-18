"""
LLM Market Analyzer
===================
Verwendet Claude/OpenAI für Marktregime-Erkennung basierend auf News.
"""

import anthropic
from openai import AsyncOpenAI
from dataclasses import dataclass
from enum import Enum
from typing import Optional, Literal
import json
import logging
from datetime import datetime

from news.news_aggregator import MarketNews, NewsAggregator

logger = logging.getLogger(__name__)


class MarketRegime(Enum):
    """Marktregime für Strategieauswahl"""
    TRENDING_BULLISH = "trending_bullish"      # → Momentum Long
    TRENDING_BEARISH = "trending_bearish"      # → Momentum Short / Hedge
    RANGE_BOUND = "range_bound"                # → Mean Reversion
    HIGH_UNCERTAINTY = "high_uncertainty"      # → Reduzierte Exposition
    CRISIS = "crisis"                          # → Cash / Defensive


@dataclass
class SectorRecommendation:
    """Sektor-Empfehlung"""
    sector: str
    stance: Literal["overweight", "neutral", "underweight", "avoid"]
    reason: str


@dataclass
class RegimeAnalysis:
    """Ergebnis der Marktregime-Analyse"""
    regime: MarketRegime
    confidence: float  # 0.0 - 1.0
    reasoning: str
    
    # Strategie-Empfehlungen
    recommended_strategy: Literal["momentum", "mean_reversion", "hedge", "cash"]
    position_size_modifier: float  # 0.0 - 1.0 (1.0 = volle Größe)
    
    # Sektor-Allokation
    sector_recommendations: list[SectorRecommendation]
    
    # Risiko-Einschätzung
    risk_level: Literal["low", "medium", "high", "extreme"]
    key_risks: list[str]
    
    # Zeithorizont
    outlook_horizon: str  # z.B. "1-2 Wochen"
    key_events_ahead: list[str]
    
    # Meta
    analysis_time: datetime
    news_count: int
    
    def to_dict(self) -> dict:
        return {
            "regime": self.regime.value,
            "confidence": self.confidence,
            "reasoning": self.reasoning,
            "recommended_strategy": self.recommended_strategy,
            "position_size_modifier": self.position_size_modifier,
            "sector_recommendations": [
                {"sector": s.sector, "stance": s.stance, "reason": s.reason}
                for s in self.sector_recommendations
            ],
            "risk_level": self.risk_level,
            "key_risks": self.key_risks,
            "outlook_horizon": self.outlook_horizon,
            "key_events_ahead": self.key_events_ahead,
            "analysis_time": self.analysis_time.isoformat(),
            "news_count": self.news_count
        }


class LLMProvider(Enum):
    ANTHROPIC = "anthropic"
    OPENAI = "openai"


class MarketAnalyzer:
    """
    KI-gestützte Marktanalyse für Regime-Erkennung
    """
    
    ANALYSIS_PROMPT = """Du bist ein erfahrener Marktanalyst. Analysiere die folgenden Marktnachrichten 
und bestimme das aktuelle Marktregime.

## AKTUELLE MARKTNACHRICHTEN:
{news_content}

## ZUSÄTZLICHE MARKTDATEN:
- VIX (Volatilitätsindex): {vix_level}
- S&P 500 Trend (20 Tage): {sp500_trend}
- Aktuelles Datum: {current_date}

## AUFGABE:
Analysiere die Nachrichtenlage und bestimme:

1. **MARKTREGIME** - Wähle eines:
   - TRENDING_BULLISH: Klarer Aufwärtstrend, positive Nachrichten dominieren, Risk-On
   - TRENDING_BEARISH: Klarer Abwärtstrend, negative Nachrichten, Risk-Off
   - RANGE_BOUND: Gemischte Signale, keine klare Richtung, Konsolidierung
   - HIGH_UNCERTAINTY: Widersprüchliche Nachrichten, wichtige Events bevorstehen
   - CRISIS: Akute Krise, Panik, systemische Risiken

2. **STRATEGIE-EMPFEHLUNG**:
   - momentum: Trendfolge (bei TRENDING_BULLISH oder TRENDING_BEARISH)
   - mean_reversion: Rückkehr zum Mittelwert (bei RANGE_BOUND)
   - hedge: Absicherung (bei HIGH_UNCERTAINTY mit bearisher Tendenz)
   - cash: Liquidität halten (bei CRISIS)

3. **SEKTOR-ANALYSE**: Welche Sektoren bevorzugen/meiden?

4. **RISIKO-BEWERTUNG**: Welche Risiken sind relevant?

## ANTWORT-FORMAT (JSON):
{{
    "regime": "TRENDING_BULLISH|TRENDING_BEARISH|RANGE_BOUND|HIGH_UNCERTAINTY|CRISIS",
    "confidence": 0.0-1.0,
    "reasoning": "Kurze Begründung (2-3 Sätze)",
    "recommended_strategy": "momentum|mean_reversion|hedge|cash",
    "position_size_modifier": 0.0-1.0,
    "sector_recommendations": [
        {{"sector": "technology", "stance": "overweight|neutral|underweight|avoid", "reason": "..."}},
        {{"sector": "financials", "stance": "...", "reason": "..."}}
    ],
    "risk_level": "low|medium|high|extreme",
    "key_risks": ["Risiko 1", "Risiko 2"],
    "outlook_horizon": "z.B. 1-2 Wochen",
    "key_events_ahead": ["Event 1", "Event 2"]
}}

Antworte NUR mit dem JSON-Objekt, ohne zusätzlichen Text."""

    def __init__(self, 
                 provider: LLMProvider = LLMProvider.ANTHROPIC,
                 api_key: Optional[str] = None,
                 model: Optional[str] = None):
        """
        Initialisiert den MarketAnalyzer
        
        Args:
            provider: LLM Provider (anthropic oder openai)
            api_key: API Key (oder aus Umgebungsvariable)
            model: Modellname (optional, nutzt Standard)
        """
        self.provider = provider
        
        if provider == LLMProvider.ANTHROPIC:
            self.client = anthropic.Anthropic(api_key=api_key)
            self.model = model or "claude-sonnet-4-20250514"
        else:
            self.client = AsyncOpenAI(api_key=api_key)
            self.model = model or "gpt-4-turbo-preview"
    
    async def analyze_market(self,
                              market_news: MarketNews,
                              vix_level: float = 20.0,
                              sp500_trend: str = "neutral") -> RegimeAnalysis:
        """
        Analysiert Marktlage und bestimmt Regime
        
        Args:
            market_news: Aggregierte Marktnachrichten
            vix_level: Aktueller VIX-Wert
            sp500_trend: S&P 500 Trend ("bullish", "bearish", "neutral")
        
        Returns:
            RegimeAnalysis mit Empfehlungen
        """
        # News für LLM formatieren
        news_content = self._format_news_for_analysis(market_news)
        
        # Prompt zusammenbauen
        prompt = self.ANALYSIS_PROMPT.format(
            news_content=news_content,
            vix_level=vix_level,
            sp500_trend=sp500_trend,
            current_date=datetime.now().strftime("%Y-%m-%d %H:%M")
        )
        
        # LLM aufrufen
        if self.provider == LLMProvider.ANTHROPIC:
            response = await self._call_anthropic(prompt)
        else:
            response = await self._call_openai(prompt)
        
        # Antwort parsen
        return self._parse_response(response, market_news)
    
    async def _call_anthropic(self, prompt: str) -> str:
        """Ruft Claude API auf"""
        try:
            message = self.client.messages.create(
                model=self.model,
                max_tokens=2000,
                messages=[
                    {"role": "user", "content": prompt}
                ]
            )
            return message.content[0].text
        except Exception as e:
            logger.error(f"Anthropic API error: {e}")
            raise
    
    async def _call_openai(self, prompt: str) -> str:
        """Ruft OpenAI API auf"""
        try:
            response = await self.client.chat.completions.create(
                model=self.model,
                messages=[
                    {"role": "user", "content": prompt}
                ],
                max_tokens=2000,
                response_format={"type": "json_object"}
            )
            return response.choices[0].message.content
        except Exception as e:
            logger.error(f"OpenAI API error: {e}")
            raise
    
    def _format_news_for_analysis(self, market_news: MarketNews, max_articles: int = 25) -> str:
        """Formatiert News kompakt für LLM"""
        recent = market_news.get_recent(hours=48)[:max_articles]
        
        lines = []
        lines.append(f"Anzahl Artikel: {len(recent)}")
        lines.append(f"Gesamt-Sentiment-Score: {market_news.overall_sentiment:+.2f}")
        lines.append(f"Trending Symbole: {', '.join(market_news.trending_symbols[:10])}")
        lines.append(f"Hauptthemen: {', '.join(market_news.key_themes)}")
        lines.append("\n--- Headlines mit Sentiment ---\n")
        
        for article in recent:
            sentiment = ""
            if article.sentiment_score is not None:
                sentiment = f" [{article.sentiment_score:+.2f}]"
            
            symbols = ""
            if article.symbols:
                symbols = f" ({', '.join(article.symbols[:2])})"
            
            lines.append(f"• {article.headline}{sentiment}{symbols}")
        
        return "\n".join(lines)
    
    def _parse_response(self, response: str, market_news: MarketNews) -> RegimeAnalysis:
        """Parst LLM-Antwort in RegimeAnalysis"""
        try:
            # JSON extrahieren (falls in Markdown-Block)
            if "```json" in response:
                response = response.split("```json")[1].split("```")[0]
            elif "```" in response:
                response = response.split("```")[1].split("```")[0]
            
            data = json.loads(response.strip())
            
            # Regime parsen
            regime_str = data.get("regime", "RANGE_BOUND").upper()
            regime = MarketRegime[regime_str]
            
            # Sektor-Empfehlungen parsen
            sector_recs = []
            for sr in data.get("sector_recommendations", []):
                sector_recs.append(SectorRecommendation(
                    sector=sr.get("sector", "unknown"),
                    stance=sr.get("stance", "neutral"),
                    reason=sr.get("reason", "")
                ))
            
            return RegimeAnalysis(
                regime=regime,
                confidence=float(data.get("confidence", 0.5)),
                reasoning=data.get("reasoning", ""),
                recommended_strategy=data.get("recommended_strategy", "mean_reversion"),
                position_size_modifier=float(data.get("position_size_modifier", 0.5)),
                sector_recommendations=sector_recs,
                risk_level=data.get("risk_level", "medium"),
                key_risks=data.get("key_risks", []),
                outlook_horizon=data.get("outlook_horizon", "1-2 Wochen"),
                key_events_ahead=data.get("key_events_ahead", []),
                analysis_time=datetime.now(),
                news_count=len(market_news.articles)
            )
            
        except Exception as e:
            logger.error(f"Error parsing LLM response: {e}")
            logger.debug(f"Raw response: {response}")
            
            # Fallback: Neutrales Regime
            return RegimeAnalysis(
                regime=MarketRegime.HIGH_UNCERTAINTY,
                confidence=0.3,
                reasoning=f"Analyse-Fehler: {str(e)}",
                recommended_strategy="cash",
                position_size_modifier=0.25,
                sector_recommendations=[],
                risk_level="high",
                key_risks=["Analyse konnte nicht durchgeführt werden"],
                outlook_horizon="unbekannt",
                key_events_ahead=[],
                analysis_time=datetime.now(),
                news_count=len(market_news.articles)
            )


class SymbolAnalyzer:
    """
    Analysiert einzelne Symbole für Trading-Entscheidungen
    """
    
    SYMBOL_PROMPT = """Du bist ein Aktienanalyst. Analysiere die folgenden Nachrichten für {symbol}.

## NACHRICHTEN ZU {symbol}:
{news_content}

## AKTUELLES MARKTREGIME: {regime}

## TECHNISCHE DATEN:
- Aktueller Kurs: {current_price}
- 20-Tage-SMA: {sma_20}
- RSI (14): {rsi}
- Volumen vs. Durchschnitt: {volume_ratio}x

## AUFGABE:
Gib eine Trading-Empfehlung basierend auf News und technischen Daten.

## ANTWORT-FORMAT (JSON):
{{
    "recommendation": "strong_buy|buy|hold|sell|strong_sell",
    "confidence": 0.0-1.0,
    "reasoning": "Begründung",
    "entry_strategy": "momentum|mean_reversion|none",
    "key_catalysts": ["Katalysator 1", "Katalysator 2"],
    "risks": ["Risiko 1", "Risiko 2"],
    "time_horizon": "kurzfristig|mittelfristig|langfristig",
    "stop_loss_suggestion_pct": 0.0-0.1,
    "target_price_suggestion_pct": 0.0-0.3
}}

Antworte NUR mit dem JSON-Objekt."""

    def __init__(self, 
                 provider: LLMProvider = LLMProvider.ANTHROPIC,
                 api_key: Optional[str] = None):
        self.provider = provider
        
        if provider == LLMProvider.ANTHROPIC:
            self.client = anthropic.Anthropic(api_key=api_key)
            self.model = "claude-sonnet-4-20250514"
        else:
            self.client = AsyncOpenAI(api_key=api_key)
            self.model = "gpt-4-turbo-preview"
    
    async def analyze_symbol(self,
                              symbol: str,
                              news: MarketNews,
                              regime: MarketRegime,
                              technical_data: dict) -> dict:
        """
        Analysiert ein Symbol für Trading-Entscheidung
        
        Args:
            symbol: Ticker-Symbol
            news: Relevante Nachrichten
            regime: Aktuelles Marktregime
            technical_data: Dict mit current_price, sma_20, rsi, volume_ratio
        """
        # News für dieses Symbol filtern
        symbol_news = news.get_for_symbol(symbol)
        
        if not symbol_news:
            return {
                "recommendation": "hold",
                "confidence": 0.3,
                "reasoning": "Keine relevanten Nachrichten gefunden",
                "entry_strategy": "none"
            }
        
        # News formatieren
        news_lines = []
        for article in symbol_news[:10]:
            sentiment = f" [{article.sentiment_score:+.2f}]" if article.sentiment_score else ""
            news_lines.append(f"• {article.headline}{sentiment}")
        
        prompt = self.SYMBOL_PROMPT.format(
            symbol=symbol,
            news_content="\n".join(news_lines),
            regime=regime.value,
            current_price=technical_data.get("current_price", "N/A"),
            sma_20=technical_data.get("sma_20", "N/A"),
            rsi=technical_data.get("rsi", "N/A"),
            volume_ratio=technical_data.get("volume_ratio", "N/A")
        )
        
        # LLM aufrufen
        if self.provider == LLMProvider.ANTHROPIC:
            message = self.client.messages.create(
                model=self.model,
                max_tokens=1000,
                messages=[{"role": "user", "content": prompt}]
            )
            response = message.content[0].text
        else:
            result = await self.client.chat.completions.create(
                model=self.model,
                messages=[{"role": "user", "content": prompt}],
                max_tokens=1000,
                response_format={"type": "json_object"}
            )
            response = result.choices[0].message.content
        
        # Parsen
        try:
            if "```json" in response:
                response = response.split("```json")[1].split("```")[0]
            return json.loads(response.strip())
        except:
            return {
                "recommendation": "hold",
                "confidence": 0.3,
                "reasoning": "Analyse-Fehler",
                "entry_strategy": "none"
            }


# Convenience-Funktionen
async def quick_regime_analysis(news_config: dict, 
                                 llm_provider: LLMProvider = LLMProvider.ANTHROPIC) -> RegimeAnalysis:
    """
    Schnelle Regime-Analyse mit News-Abruf
    """
    # News abrufen
    aggregator = NewsAggregator(news_config)
    try:
        news = await aggregator.get_market_news()
    finally:
        await aggregator.close()
    
    # Analysieren
    analyzer = MarketAnalyzer(provider=llm_provider)
    return await analyzer.analyze_market(news)


# Demo/Test
if __name__ == "__main__":
    import asyncio
    import os
    
    async def demo():
        # Konfiguration aus Umgebungsvariablen
        news_config = {
            "finnhub_key": os.getenv("FINNHUB_API_KEY"),
            "alpha_vantage_key": os.getenv("ALPHA_VANTAGE_API_KEY"),
        }
        
        print("=== Market Regime Analysis Demo ===\n")
        
        # Regime analysieren
        analysis = await quick_regime_analysis(news_config)
        
        print(f"Regime: {analysis.regime.value}")
        print(f"Konfidenz: {analysis.confidence:.0%}")
        print(f"Strategie: {analysis.recommended_strategy}")
        print(f"Position Size Modifier: {analysis.position_size_modifier:.0%}")
        print(f"\nBegründung: {analysis.reasoning}")
        print(f"\nRisiko-Level: {analysis.risk_level}")
        print(f"Key Risks: {', '.join(analysis.key_risks)}")
        print(f"\nSektor-Empfehlungen:")
        for sr in analysis.sector_recommendations:
            print(f"  - {sr.sector}: {sr.stance} ({sr.reason})")
    
    asyncio.run(demo())
