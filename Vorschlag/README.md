# 🤖 AI Trading Bot für CapTrader

Ein KI-gestützter Trading-Bot, der automatisch zwischen **Momentum**- und **Mean-Reversion**-Strategien wechselt, basierend auf Echtzeit-Nachrichtenanalyse durch Large Language Models.

## 🎯 Konzept

```
News-Feeds → LLM-Analyse → Marktregime → Strategie-Auswahl → Trading
```

Der Bot analysiert kontinuierlich Finanznachrichten und bestimmt das aktuelle Marktregime:

| Regime | Strategie | Beschreibung |
|--------|-----------|--------------|
| 📈 Trending Bullish | Momentum Long | Klare Aufwärtstrends folgen |
| 📉 Trending Bearish | Momentum Short/Hedge | Abwärtstrends handeln |
| ↔️ Range Bound | Mean Reversion | Überverkaufte Werte kaufen |
| ⚠️ High Uncertainty | Reduziert | Positionen verkleinern |
| 🚨 Crisis | Cash | Kapital sichern |

## ✨ Features

- **Meta-Strategie**: KI wählt automatisch die beste Strategie für die aktuelle Marktlage
- **Multi-Source News**: Aggregiert News von Finnhub, Alpha Vantage, NewsAPI und RSS
- **LLM-Analyse**: Nutzt Claude oder GPT-4 für Sentiment- und Regime-Erkennung
- **Risikomanagement**: Automatisches Position Sizing mit Gebühren-Berücksichtigung
- **CapTrader/IB Integration**: Direkte Anbindung via IB API

## 🚀 Quick Start

### 1. Repository klonen

```bash
git clone https://github.com/your-repo/ai-trading-bot.git
cd ai-trading-bot
```

### 2. Dependencies installieren

```bash
python -m venv venv
source venv/bin/activate  # Windows: .\venv\Scripts\activate
pip install -r requirements.txt
```

### 3. Konfiguration erstellen

```bash
cp config/config.example.yaml config/config.yaml
cp config/secrets.example.yaml config/secrets.yaml
```

Dann `config/secrets.yaml` mit deinen API-Keys füllen:

```yaml
finnhub_api_key: "dein_key"
anthropic_api_key: "dein_key"
```

### 4. IB Gateway starten

Starte die Trader Workstation (TWS) oder IB Gateway und aktiviere die API-Verbindung.

### 5. Bot starten (Paper Trading)

```bash
python -m src.main --mode paper
```

## ⚠️ Wichtige Hinweise

> **ACHTUNG**: Dieser Bot handelt mit echtem Geld. Der Autor übernimmt keine Verantwortung für Verluste.

1. **Paper Trading zuerst**: Teste mindestens 2-3 Monate im Paper-Trading-Modus
2. **Nur Risikokapital**: Setze nur Geld ein, dessen Verlust du verkraften kannst
3. **Überwachung**: Lasse den Bot nicht unbeaufsichtigt laufen
4. **API-Kosten**: LLM-Aufrufe verursachen Kosten (ca. $0.01-0.05 pro Analyse)

## 📁 Projektstruktur

```
ai-trading-bot/
├── CLAUDE.md              # Detaillierte Projekt-Dokumentation
├── README.md              # Diese Datei
├── requirements.txt       # Python Dependencies
├── config/
│   ├── config.yaml        # Hauptkonfiguration
│   └── secrets.yaml       # API Keys (nicht committen!)
├── src/
│   ├── bot.py             # Haupt-Trading-Bot
│   ├── news/              # News-Aggregation
│   ├── analysis/          # LLM Marktanalyse
│   ├── risk/              # Risikomanagement
│   ├── strategies/        # Trading-Strategien
│   └── execution/         # Order-Management
└── data/
    ├── cache/             # News-Cache
    └── logs/              # Trading-Logs
```

## 🔧 Konfiguration

### Risiko-Parameter

```yaml
risk:
  max_position_pct: 0.05      # Max 5% pro Position
  max_risk_per_trade_pct: 0.01 # Max 1% Risiko pro Trade
  max_drawdown_pct: 0.15      # Stopp bei 15% Drawdown
```

### Strategien

```yaml
strategies:
  momentum:
    stop_loss_pct: 0.05       # 5% Stop-Loss
    take_profit_pct: 0.15     # 15% Take-Profit
    
  mean_reversion:
    stop_loss_pct: 0.03       # 3% Stop-Loss
    take_profit_pct: 0.06     # 6% Take-Profit
```

## 📊 Gebühren (CapTrader)

| Börse | Gebühr | Minimum |
|-------|--------|---------|
| Xetra | 0.10% | 4.00 € |
| NYSE/NASDAQ | $0.01/Aktie | $2.00 |

Der Bot berücksichtigt Gebühren automatisch und verwirft Trades, die nicht profitabel sein können.

## 🤝 Beitragen

Pull Requests sind willkommen! Bitte erst ein Issue erstellen, um größere Änderungen zu diskutieren.

## 📜 Lizenz

MIT License - siehe [LICENSE](LICENSE)

## 📚 Ressourcen

- [CapTrader](https://www.captrader.com/)
- [Interactive Brokers API](https://interactivebrokers.github.io/tws-api/)
- [ib_insync Dokumentation](https://ib-insync.readthedocs.io/)
- [Anthropic Claude API](https://docs.anthropic.com/)
