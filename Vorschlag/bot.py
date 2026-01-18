"""
AI Trading Bot - Hauptklasse
============================
Orchestriert alle Module und führt den Trading-Zyklus aus.
"""

import asyncio
import yaml
from datetime import datetime, time
from pathlib import Path
from typing import Optional
from dataclasses import dataclass
import logging

from ib_insync import IB, Stock, Contract, MarketOrder, LimitOrder
from loguru import logger

from news.news_aggregator import NewsAggregator, MarketNews
from analysis.market_analyzer import MarketAnalyzer, MarketRegime, RegimeAnalysis, LLMProvider
from risk.risk_manager import RiskManager, PositionSizeResult
from risk.cost_calculator import CostCalculator


@dataclass
class BotConfig:
    """Bot-Konfiguration aus YAML"""
    capital: float
    mode: str  # "paper" oder "live"
    cycle_interval: int
    min_trade_capital: float
    
    # IB Gateway
    ib_host: str
    ib_port: int
    ib_client_id: int
    
    # Risk
    max_position_pct: float
    max_risk_per_trade_pct: float
    max_portfolio_risk_pct: float
    max_drawdown_pct: float
    
    # LLM
    llm_provider: str
    llm_model: str
    
    # Trading Hours
    trading_start: time
    trading_end: time
    timezone: str
    
    @classmethod
    def from_yaml(cls, config_path: str, secrets_path: str) -> "BotConfig":
        """Lädt Konfiguration aus YAML-Dateien"""
        with open(config_path) as f:
            config = yaml.safe_load(f)
        
        with open(secrets_path) as f:
            secrets = yaml.safe_load(f)
        
        trading = config.get("trading", {})
        risk = config.get("risk", {})
        llm = config.get("llm", {})
        hours = config.get("markets", {}).get("trading_hours", {})
        ib = trading.get("ib_gateway", {})
        
        return cls(
            capital=trading.get("capital", 50000),
            mode=trading.get("mode", "paper"),
            cycle_interval=trading.get("cycle_interval_seconds", 300),
            min_trade_capital=trading.get("min_trade_capital", 500),
            ib_host=ib.get("host", "127.0.0.1"),
            ib_port=ib.get("port", 7497),
            ib_client_id=ib.get("client_id", 1),
            max_position_pct=risk.get("max_position_pct", 0.05),
            max_risk_per_trade_pct=risk.get("max_risk_per_trade_pct", 0.01),
            max_portfolio_risk_pct=risk.get("max_portfolio_risk_pct", 0.10),
            max_drawdown_pct=risk.get("max_drawdown_pct", 0.15),
            llm_provider=llm.get("provider", "anthropic"),
            llm_model=llm.get("model", "claude-sonnet-4-20250514"),
            trading_start=time.fromisoformat(hours.get("start", "09:00")),
            trading_end=time.fromisoformat(hours.get("end", "17:30")),
            timezone=hours.get("timezone", "Europe/Berlin")
        )


class AITradingBot:
    """
    Haupt-Trading-Bot mit KI-gestützter Meta-Strategie
    """
    
    def __init__(self, config: BotConfig, secrets: dict):
        self.config = config
        self.secrets = secrets
        
        # IB Verbindung
        self.ib = IB()
        
        # Module initialisieren
        self.news_aggregator = NewsAggregator({
            "finnhub_key": secrets.get("finnhub_api_key"),
            "alpha_vantage_key": secrets.get("alpha_vantage_api_key"),
            "newsapi_key": secrets.get("newsapi_key"),
        })
        
        self.market_analyzer = MarketAnalyzer(
            provider=LLMProvider.ANTHROPIC if config.llm_provider == "anthropic" else LLMProvider.OPENAI,
            api_key=secrets.get("anthropic_api_key") or secrets.get("openai_api_key")
        )
        
        self.risk_manager = RiskManager(
            total_capital=config.capital,
            max_position_pct=config.max_position_pct,
            max_risk_per_trade_pct=config.max_risk_per_trade_pct,
            max_portfolio_risk_pct=config.max_portfolio_risk_pct
        )
        
        self.cost_calculator = CostCalculator()
        
        # State
        self.is_running = False
        self.current_regime: Optional[RegimeAnalysis] = None
        self.cycle_count = 0
        
        logger.info(f"Bot initialisiert - Modus: {config.mode}, Kapital: {config.capital:,.2f}€")
    
    async def start(self):
        """Startet den Trading-Bot"""
        logger.info("=== AI Trading Bot startet ===")
        
        # IB Verbindung herstellen
        try:
            await self.ib.connectAsync(
                self.config.ib_host,
                self.config.ib_port,
                clientId=self.config.ib_client_id
            )
            logger.success(f"Verbunden mit IB Gateway ({self.config.ib_host}:{self.config.ib_port})")
        except Exception as e:
            logger.error(f"IB Verbindung fehlgeschlagen: {e}")
            raise
        
        self.is_running = True
        
        # Hauptschleife
        while self.is_running:
            try:
                # Handelszeiten prüfen
                if not self._is_trading_hours():
                    logger.info("Außerhalb der Handelszeiten - warte...")
                    await asyncio.sleep(60)
                    continue
                
                # Trading-Zyklus ausführen
                await self._trading_cycle()
                
                # Warten bis zum nächsten Zyklus
                logger.info(f"Nächster Zyklus in {self.config.cycle_interval} Sekunden...")
                await asyncio.sleep(self.config.cycle_interval)
                
            except KeyboardInterrupt:
                logger.info("Bot durch Benutzer gestoppt")
                break
            except Exception as e:
                logger.error(f"Fehler im Trading-Zyklus: {e}")
                await asyncio.sleep(60)
        
        await self.shutdown()
    
    async def shutdown(self):
        """Fährt den Bot sauber herunter"""
        logger.info("Bot wird heruntergefahren...")
        self.is_running = False
        
        # News-Aggregator schließen
        await self.news_aggregator.close()
        
        # IB Verbindung trennen
        if self.ib.isConnected():
            self.ib.disconnect()
        
        logger.success("Bot erfolgreich beendet")
    
    async def _trading_cycle(self):
        """Führt einen kompletten Trading-Zyklus aus"""
        self.cycle_count += 1
        logger.info(f"\n{'='*60}")
        logger.info(f"=== Trading-Zyklus #{self.cycle_count} - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')} ===")
        logger.info(f"{'='*60}\n")
        
        # 1. News abrufen
        logger.info("📰 Rufe Marktnachrichten ab...")
        try:
            market_news = await self.news_aggregator.get_market_news()
            logger.info(f"   Gefunden: {len(market_news.articles)} Artikel")
            logger.info(f"   Gesamt-Sentiment: {market_news.overall_sentiment:+.2f}")
        except Exception as e:
            logger.error(f"News-Abruf fehlgeschlagen: {e}")
            return
        
        # 2. Marktdaten holen (VIX etc.)
        vix_level = await self._get_vix_level()
        logger.info(f"📊 VIX: {vix_level:.1f}")
        
        # 3. Marktregime analysieren
        logger.info("🤖 Analysiere Marktregime mit LLM...")
        try:
            self.current_regime = await self.market_analyzer.analyze_market(
                market_news=market_news,
                vix_level=vix_level
            )
            
            logger.info(f"   Regime: {self.current_regime.regime.value}")
            logger.info(f"   Konfidenz: {self.current_regime.confidence:.0%}")
            logger.info(f"   Strategie: {self.current_regime.recommended_strategy}")
            logger.info(f"   Begründung: {self.current_regime.reasoning}")
        except Exception as e:
            logger.error(f"Regime-Analyse fehlgeschlagen: {e}")
            return
        
        # 4. Portfolio-Status prüfen
        portfolio = self.ib.portfolio()
        account_values = self.ib.accountValues()
        available_cash = self._get_available_cash(account_values)
        current_risk = self._calculate_portfolio_risk(portfolio)
        
        logger.info(f"\n💰 Portfolio-Status:")
        logger.info(f"   Verfügbares Kapital: {available_cash:,.2f}€")
        logger.info(f"   Positionen: {len(portfolio)}")
        logger.info(f"   Portfolio-Risiko: {current_risk:.1%}")
        
        # 5. Bestehende Positionen managen
        if portfolio:
            logger.info("\n📋 Prüfe bestehende Positionen...")
            await self._manage_existing_positions(portfolio)
        
        # 6. Neue Opportunities suchen
        if self._should_seek_opportunities(available_cash):
            logger.info("\n🔍 Suche neue Handelsmöglichkeiten...")
            await self._seek_new_opportunities(available_cash, portfolio)
        else:
            if self.current_regime.regime == MarketRegime.CRISIS:
                logger.warning("⚠️ CRISIS-Modus: Keine neuen Positionen")
            elif available_cash < self.config.min_trade_capital:
                logger.info("💵 Nicht genug Kapital für neue Trades")
        
        logger.info(f"\n{'='*60}")
        logger.info(f"=== Zyklus #{self.cycle_count} beendet ===")
        logger.info(f"{'='*60}\n")
    
    async def _manage_existing_positions(self, portfolio: list):
        """Prüft bestehende Positionen auf Exit-Signale"""
        for position in portfolio:
            symbol = position.contract.symbol
            current_price = position.marketPrice
            avg_cost = position.averageCost
            quantity = position.position
            unrealized_pnl = position.unrealizedPNL
            unrealized_pnl_pct = (current_price - avg_cost) / avg_cost if avg_cost > 0 else 0
            
            logger.info(f"   {symbol}: {quantity} Stück @ {avg_cost:.2f}€ → {current_price:.2f}€ ({unrealized_pnl_pct:+.1%})")
            
            # Exit-Logik prüfen
            should_exit, reason = self._check_exit_conditions(position, unrealized_pnl_pct)
            
            if should_exit:
                logger.warning(f"   ⚡ EXIT {symbol}: {reason}")
                await self._close_position(position)
    
    def _check_exit_conditions(self, position, unrealized_pnl_pct: float) -> tuple[bool, str]:
        """Prüft ob eine Position geschlossen werden soll"""
        # Regime-basierte Exits
        if self.current_regime.regime == MarketRegime.CRISIS:
            return True, "Marktregime: CRISIS"
        
        # Standard Stop-Loss/Take-Profit
        strategy = self.current_regime.recommended_strategy
        
        if strategy == "momentum":
            stop_loss = -0.05
            take_profit = 0.15
        else:  # mean_reversion
            stop_loss = -0.03
            take_profit = 0.06
        
        if unrealized_pnl_pct <= stop_loss:
            return True, f"Stop-Loss erreicht ({unrealized_pnl_pct:.1%})"
        
        if unrealized_pnl_pct >= take_profit:
            return True, f"Take-Profit erreicht ({unrealized_pnl_pct:.1%})"
        
        return False, ""
    
    async def _close_position(self, position):
        """Schließt eine Position"""
        contract = position.contract
        quantity = abs(position.position)
        action = "SELL" if position.position > 0 else "BUY"
        
        order = MarketOrder(action, quantity)
        trade = self.ib.placeOrder(contract, order)
        
        logger.info(f"   Order platziert: {action} {quantity} {contract.symbol}")
        
        # Auf Ausführung warten
        while not trade.isDone():
            await asyncio.sleep(0.1)
        
        if trade.orderStatus.status == "Filled":
            logger.success(f"   ✓ Position geschlossen @ {trade.orderStatus.avgFillPrice:.2f}€")
        else:
            logger.error(f"   ✗ Order fehlgeschlagen: {trade.orderStatus.status}")
    
    async def _seek_new_opportunities(self, available_cash: float, portfolio: list):
        """Sucht neue Handelsmöglichkeiten"""
        # Hier würde der MarketScanner integriert
        # Für jetzt: Einfache Watchlist-basierte Logik
        
        watchlist = ["AAPL", "MSFT", "GOOGL"]  # TODO: Aus Config laden
        held_symbols = [p.contract.symbol for p in portfolio]
        
        for symbol in watchlist:
            if symbol in held_symbols:
                continue
            
            # Aktuellen Preis holen
            contract = Stock(symbol, "SMART", "USD")
            self.ib.qualifyContracts(contract)
            
            ticker = self.ib.reqMktData(contract, "", False, False)
            await asyncio.sleep(1)
            
            if ticker.last is None or ticker.last <= 0:
                self.ib.cancelMktData(contract)
                continue
            
            entry_price = ticker.last
            
            # Position Sizing
            sizing = self.risk_manager.calculate_position_size(
                symbol=symbol,
                entry_price=entry_price,
                strategy=self.current_regime.recommended_strategy,
                signal_strength=self.current_regime.confidence,
                exchange="SMART",
                current_portfolio_risk=self._calculate_portfolio_risk(portfolio)
            )
            
            if sizing.viable:
                logger.info(f"   📈 Kandidat: {symbol}")
                logger.info(f"      Preis: {entry_price:.2f}€")
                logger.info(f"      Stückzahl: {sizing.shares}")
                logger.info(f"      Stop-Loss: {sizing.stop_loss_price:.2f}€")
                logger.info(f"      Take-Profit: {sizing.take_profit_price:.2f}€")
                
                # TODO: Hier würde die Order platziert werden
                # await self._open_position(contract, sizing)
            else:
                logger.debug(f"   ⏭️ {symbol} übersprungen: {sizing.reason}")
            
            self.ib.cancelMktData(contract)
    
    async def _get_vix_level(self) -> float:
        """Holt aktuellen VIX-Wert"""
        try:
            # VIX Index
            contract = self.ib.qualifyContracts(
                Contract(symbol="VIX", secType="IND", exchange="CBOE")
            )[0]
            
            ticker = self.ib.reqMktData(contract, "", False, False)
            await asyncio.sleep(1)
            
            vix = ticker.last if ticker.last else 20.0  # Default
            self.ib.cancelMktData(contract)
            
            return vix
        except:
            return 20.0  # Default bei Fehler
    
    def _get_available_cash(self, account_values: list) -> float:
        """Extrahiert verfügbares Kapital aus Account-Daten"""
        for av in account_values:
            if av.tag == "AvailableFunds" and av.currency == "EUR":
                return float(av.value)
        return 0.0
    
    def _calculate_portfolio_risk(self, portfolio: list) -> float:
        """Berechnet aktuelles Portfolio-Risiko"""
        if not portfolio:
            return 0.0
        
        total_risk = 0.0
        for position in portfolio:
            # Vereinfachte Risiko-Berechnung
            position_value = abs(position.position * position.marketPrice)
            risk_per_position = position_value * 0.05  # Annahme: 5% Stop-Loss
            total_risk += risk_per_position
        
        return total_risk / self.config.capital
    
    def _should_seek_opportunities(self, available_cash: float) -> bool:
        """Prüft ob neue Positionen gesucht werden sollen"""
        if self.current_regime.regime == MarketRegime.CRISIS:
            return False
        
        if available_cash < self.config.min_trade_capital:
            return False
        
        if self.current_regime.position_size_modifier < 0.25:
            return False
        
        return True
    
    def _is_trading_hours(self) -> bool:
        """Prüft ob gerade Handelszeit ist"""
        now = datetime.now().time()
        return self.config.trading_start <= now <= self.config.trading_end


# Entry Point
async def main():
    """Haupteinstiegspunkt"""
    import os
    
    # Pfade
    config_path = Path("config/config.yaml")
    secrets_path = Path("config/secrets.yaml")
    
    # Prüfen ob Dateien existieren
    if not config_path.exists():
        logger.error(f"Config nicht gefunden: {config_path}")
        logger.info("Kopiere config/config.example.yaml nach config/config.yaml")
        return
    
    if not secrets_path.exists():
        logger.error(f"Secrets nicht gefunden: {secrets_path}")
        logger.info("Kopiere config/secrets.example.yaml nach config/secrets.yaml")
        return
    
    # Konfiguration laden
    config = BotConfig.from_yaml(str(config_path), str(secrets_path))
    
    with open(secrets_path) as f:
        secrets = yaml.safe_load(f)
    
    # Bot starten
    bot = AITradingBot(config, secrets)
    
    try:
        await bot.start()
    except KeyboardInterrupt:
        logger.info("Beende Bot...")
    finally:
        await bot.shutdown()


if __name__ == "__main__":
    asyncio.run(main())
