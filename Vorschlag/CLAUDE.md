# AI Trading Bot mit Meta-Strategie

## Projektübersicht

Ein KI-gestützter Trading Bot für CapTrader/Interactive Brokers, der automatisch zwischen Momentum- und Mean-Reversion-Strategien wechselt, basierend auf Nachrichten-Analyse durch LLMs (Claude/OpenAI).

### Kernkonzept

```
News-Analyse (LLM) → Marktregime → Strategie-Auswahl → Trading-Signale → Order-Execution
```

**Meta-Strategie**: Das System entscheidet basierend auf der aktuellen Nachrichtenlage, ob:
- **Momentum** (Trendfolge) bei klaren Trends
- **Mean Reversion** (Rückkehr zum Mittelwert) bei Seitwärtsmärkten  
- **Hedging/Cash** bei Unsicherheit oder Krisen

---

## Architektur

```
┌─────────────────────────────────────────────────────────────────────┐
│                     META-STRATEGIE (LLM)                            │
│  News-Aggregation → Sentiment-Analyse → Regime-Erkennung            │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │
        ┌─────────────────────────┼─────────────────────────────────┐
        ▼                         ▼                                 ▼
┌───────────────┐       ┌─────────────────┐             ┌───────────────────┐
│   MOMENTUM    │       │ MEAN REVERSION  │             │   MARKTSCANNER    │
│    Modul      │       │     Modul       │             │   (Neue Werte)    │
│               │       │                 │             │                   │
│ - Breakouts   │       │ - RSI < 30      │             │ - Top Gainers     │
│ - Trend-Folge │       │ - Bollinger     │             │ - Überverkauft    │
│ - Momentum    │       │ - Mean Return   │             │ - Sektor-Rotation │
└───────┬───────┘       └────────┬────────┘             └─────────┬─────────┘
        │                        │                                 │
        └────────────────────────┼─────────────────────────────────┘
                                 ▼
                    ┌─────────────────────────┐
                    │    RISIKO-MANAGER       │
                    │  • Position Sizing      │
                    │  • Gebühren-Check       │
                    │  • Korrelations-Check   │
                    │  • Drawdown-Limits      │
                    └────────────┬────────────┘
                                 ▼
                    ┌─────────────────────────┐
                    │   ORDER-EXECUTION       │
                    │   (IB API / ib_insync)  │
                    └─────────────────────────┘
```

---

## Projektstruktur

```
ai-trading-bot/
├── CLAUDE.md                    # Diese Datei (Projekt-Dokumentation)
├── README.md                    # Benutzer-Dokumentation
├── requirements.txt             # Python Dependencies
├── config/
│   ├── config.yaml              # Hauptkonfiguration
│   ├── secrets.yaml             # API Keys (nicht committen!)
│   └── strategies.yaml          # Strategie-Parameter
├── src/
│   ├── __init__.py
│   ├── main.py                  # Entry Point
│   ├── bot.py                   # Haupt-Trading-Bot-Klasse
│   │
│   ├── news/                    # News-Aggregation
│   │   ├── __init__.py
│   │   ├── news_aggregator.py   # Multi-Source News Aggregator
│   │   ├── finnhub_client.py    # Finnhub API
│   │   ├── alpha_vantage.py     # Alpha Vantage API
│   │   └── rss_feeds.py         # RSS Fallback
│   │
│   ├── analysis/                # Marktanalyse
│   │   ├── __init__.py
│   │   ├── market_analyzer.py   # LLM-basierte Regime-Erkennung
│   │   ├── technical.py         # Technische Indikatoren
│   │   └── sentiment.py         # Sentiment-Analyse
│   │
│   ├── strategies/              # Trading-Strategien
│   │   ├── __init__.py
│   │   ├── base_strategy.py     # Basis-Klasse
│   │   ├── momentum.py          # Momentum-Strategie
│   │   └── mean_reversion.py    # Mean-Reversion-Strategie
│   │
│   ├── execution/               # Order-Management
│   │   ├── __init__.py
│   │   ├── ib_connector.py      # IB API Verbindung
│   │   ├── order_manager.py     # Order-Logik
│   │   └── position_manager.py  # Position-Tracking
│   │
│   ├── risk/                    # Risikomanagement
│   │   ├── __init__.py
│   │   ├── risk_manager.py      # Haupt-Risiko-Logik
│   │   ├── position_sizing.py   # Position Sizing
│   │   └── cost_calculator.py   # Gebühren-Berechnung
│   │
│   ├── scanner/                 # Markt-Scanner
│   │   ├── __init__.py
│   │   └── market_scanner.py    # Findet neue Opportunities
│   │
│   └── utils/                   # Hilfsfunktionen
│       ├── __init__.py
│       ├── logger.py            # Logging-Setup
│       └── helpers.py           # Allgemeine Helfer
│
├── tests/                       # Tests
│   ├── __init__.py
│   ├── test_news.py
│   ├── test_strategies.py
│   └── test_risk.py
│
├── data/                        # Daten (nicht committen)
│   ├── cache/                   # News-Cache
│   └── logs/                    # Trading-Logs
│
└── notebooks/                   # Jupyter Notebooks für Analyse
    ├── backtest.ipynb
    └── strategy_analysis.ipynb
```

---

## Marktregimes

Das System erkennt 5 Marktregimes:

| Regime | Beschreibung | Strategie | Position Size |
|--------|--------------|-----------|---------------|
| `TRENDING_BULLISH` | Klarer Aufwärtstrend, positive News | Momentum Long | 100% |
| `TRENDING_BEARISH` | Klarer Abwärtstrend, negative News | Momentum Short/Hedge | 50-75% |
| `RANGE_BOUND` | Seitwärtsmarkt, gemischte Signale | Mean Reversion | 75% |
| `HIGH_UNCERTAINTY` | Widersprüchlich, wichtige Events | Reduziert | 25-50% |
| `CRISIS` | Panik, systemische Risiken | Cash | 0% (nur Defensive) |

---

## Gebührenstruktur (CapTrader/IB)

### Aktien

| Börse | Gebühr | Minimum | Maximum |
|-------|--------|---------|---------|
| Xetra | 0.10% | 4.00 € | 99.00 € |
| NYSE/NASDAQ | $0.01/Aktie | $2.00 | - |
| LSE | 0.10% | £6.00 | - |

### ETFs
Wie Aktien-Gebühren

### Optionen

| Markt | Gebühr pro Kontrakt |
|-------|---------------------|
| Deutschland | 2.00 € |
| USA | $0.65 |

### Sonstige
- Einzahlung: Kostenlos
- Auszahlung: 1x/Monat kostenlos, danach 8 €
- Währungswechsel: ~0.002% + Spread

**Wichtig**: Jeder Trade muss einen erwarteten Return > 2× Breakeven-Kosten haben!

---

## API Keys & Konfiguration

### Benötigte API Keys

```yaml
# config/secrets.yaml (NICHT COMMITTEN!)

# News APIs (mindestens einer empfohlen)
finnhub_api_key: "your_key"          # Kostenlos, 60 req/min
alpha_vantage_api_key: "your_key"    # Kostenlos, 25 req/tag
newsapi_key: "your_key"              # Kostenlos, 100 req/tag

# LLM (einer erforderlich)
anthropic_api_key: "your_key"        # Claude
openai_api_key: "your_key"           # GPT-4 (alternativ)

# Trading
ib_gateway_host: "127.0.0.1"
ib_gateway_port: 7497                # 7497=Paper, 7496=Live
```

### Hauptkonfiguration

```yaml
# config/config.yaml

trading:
  capital: 50000                     # Startkapital
  mode: "paper"                      # paper oder live
  cycle_interval_seconds: 300        # 5 Minuten zwischen Zyklen
  
risk:
  max_position_pct: 0.05             # Max 5% pro Position
  max_risk_per_trade_pct: 0.01       # Max 1% Risiko pro Trade
  max_portfolio_risk_pct: 0.10       # Max 10% Gesamtrisiko
  max_drawdown_pct: 0.15             # Stopp bei 15% Drawdown
  min_reward_ratio: 2.0              # Min. Reward/Risk Ratio

strategies:
  momentum:
    stop_loss_pct: 0.05              # 5% Stop-Loss
    take_profit_pct: 0.15            # 15% Take-Profit
    min_trend_strength: 0.6          # Min. Trend-Stärke
    
  mean_reversion:
    stop_loss_pct: 0.03              # 3% Stop-Loss  
    take_profit_pct: 0.06            # 6% Take-Profit
    rsi_oversold: 30                 # RSI Überverkauft
    rsi_overbought: 70               # RSI Überkauft

markets:
  allowed_exchanges: ["XETRA", "NYSE", "NASDAQ"]
  allowed_instruments: ["STK", "ETF"]
  trading_hours:
    start: "09:00"
    end: "17:30"
    timezone: "Europe/Berlin"
```

---

## Wichtige Klassen & Interfaces

### NewsAggregator
```python
# Aggregiert News aus mehreren Quellen
aggregator = NewsAggregator(config)
news = await aggregator.get_market_news()
news = await aggregator.get_symbol_news("AAPL", days_back=7)
```

### MarketAnalyzer
```python
# LLM-basierte Marktanalyse
analyzer = MarketAnalyzer(provider=LLMProvider.ANTHROPIC)
regime = await analyzer.analyze_market(news, vix_level=20.0)

# Ergebnis:
regime.regime              # MarketRegime.TRENDING_BULLISH
regime.recommended_strategy # "momentum"
regime.confidence          # 0.85
regime.sector_recommendations  # [SectorRecommendation(...), ...]
```

### RiskManager
```python
# Position Sizing mit Gebühren-Check
risk_mgr = RiskManager(capital=50000)
sizing = risk_mgr.calculate_position_size(
    symbol="AAPL",
    entry_price=150.0,
    strategy="momentum",
    signal_strength=0.8,
    exchange="NASDAQ"
)

# Ergebnis:
sizing.shares              # 50
sizing.stop_loss_price     # 142.50
sizing.take_profit_price   # 172.50
sizing.viable              # True (Gebühren OK)
```

### TradingBot (Hauptklasse)
```python
# Startet den Bot
bot = AITradingBot(config)
await bot.start()  # Läuft in Endlosschleife
```

---

## Trading-Zyklus

Jeder Zyklus (Standard: alle 5 Minuten):

1. **News abrufen** → NewsAggregator
2. **Marktregime bestimmen** → MarketAnalyzer (LLM)
3. **Bestehende Positionen prüfen**
   - Exit-Signale checken
   - Stop-Loss/Take-Profit anpassen
4. **Neue Opportunities suchen** (wenn Kapital verfügbar)
   - MarketScanner basierend auf Regime
   - Position Sizing mit Gebühren-Check
   - Order platzieren (Bracket: Entry + SL + TP)
5. **Logging & Reporting**

---

## Entwicklungs-Richtlinien

### Code-Stil
- Python 3.11+
- Type Hints überall
- Async/Await für I/O
- Dataclasses für Datenstrukturen
- Enum für feste Werte

### Fehlerbehandlung
- Alle API-Calls in try/except
- Graceful Degradation (RSS als Fallback für News)
- Logging aller Fehler
- Nie crashen im Live-Trading

### Testing
- Unit Tests für alle Strategien
- Integration Tests mit Paper Trading
- Backtesting vor Live-Einsatz

### Sicherheit
- API Keys nie committen
- Paper Trading zuerst (mindestens 2-3 Monate)
- Position Limits strikt einhalten
- Drawdown-Limits implementieren

---

## TODO / Roadmap

### Phase 1: Grundgerüst ✓
- [x] News Aggregator
- [x] LLM Market Analyzer
- [x] Gebühren-Kalkulation
- [ ] IB Connector (ib_insync)
- [ ] Basis Trading Bot

### Phase 2: Strategien
- [ ] Momentum-Strategie implementieren
- [ ] Mean-Reversion-Strategie implementieren
- [ ] Backtesting-Framework

### Phase 3: Risikomanagement
- [ ] Position Sizing komplett
- [ ] Korrelations-Check
- [ ] Drawdown-Schutz
- [ ] Portfolio-Heat-Map

### Phase 4: Scanner & Erweiterungen
- [ ] Market Scanner
- [ ] Sektor-Rotation
- [ ] Options-Modul (Absicherung)
- [ ] Hebelprodukte (mit Extra-Vorsicht)

### Phase 5: Monitoring
- [ ] Dashboard (Streamlit/Gradio)
- [ ] Telegram/Discord Alerts
- [ ] Performance-Reporting

---

## Bekannte Einschränkungen

1. **Latenz**: Retail-API nicht für HFT geeignet
2. **News-Verzögerung**: Kostenlose APIs haben Limits
3. **LLM-Kosten**: Claude API Kosten bei häufiger Nutzung
4. **Marktregime-Wechsel**: Kann zu Whipsaws führen
5. **Backtesting**: Historische News schwer zu bekommen

---

## Nützliche Befehle

```bash
# Virtuelle Umgebung erstellen
python -m venv venv
source venv/bin/activate  # Linux/Mac
.\venv\Scripts\activate   # Windows

# Dependencies installieren
pip install -r requirements.txt

# Tests ausführen
pytest tests/

# Bot starten (Paper Trading)
python -m src.main --mode paper

# Bot starten (Live - VORSICHT!)
python -m src.main --mode live
```

---

## Ressourcen

- [ib_insync Dokumentation](https://ib-insync.readthedocs.io/)
- [Interactive Brokers API](https://interactivebrokers.github.io/tws-api/)
- [CapTrader Preis-Leistungs-Verzeichnis](https://www.captrader.com/de/preise/)
- [Finnhub API Docs](https://finnhub.io/docs/api)
- [Alpha Vantage Docs](https://www.alphavantage.co/documentation/)
- [Anthropic Claude API](https://docs.anthropic.com/)

---

## Kontakt & Verantwortung

**Wichtig**: Dieser Bot handelt mit echtem Geld. Der Autor übernimmt keine Verantwortung für Verluste. Immer zuerst Paper Trading nutzen und nur Geld einsetzen, dessen Verlust verkraftbar ist.
