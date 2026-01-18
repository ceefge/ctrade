"""
Risikomanagement Module
=======================
Position Sizing, Gebühren-Kalkulation, Portfolio-Risiko
"""

from dataclasses import dataclass
from typing import Optional
import logging

logger = logging.getLogger(__name__)


@dataclass
class TradeCosts:
    """Kosten für einen Trade"""
    commission: float           # Broker-Kommission
    spread_estimate: float      # Geschätzter Spread
    total_roundtrip: float      # Gesamtkosten Kauf + Verkauf
    breakeven_move: float       # % Bewegung zum Breakeven


class CostCalculator:
    """
    Berechnet Trading-Kosten für CapTrader/IB
    """
    
    # Deutsche Aktien (Xetra)
    XETRA_RATE = 0.001      # 0.1%
    XETRA_MIN = 4.0         # mind. 4€
    XETRA_MAX = 99.0        # max. 99€
    
    # US Aktien
    US_PER_SHARE = 0.01     # 1 Cent/Aktie
    US_MIN = 2.0            # mind. 2$
    
    # UK Aktien
    UK_RATE = 0.001         # 0.1%
    UK_MIN = 6.0            # mind. 6£
    
    # Optionen
    OPTION_DE = 2.0         # 2€ pro Kontrakt
    OPTION_US = 0.65        # 0.65$ pro Kontrakt
    
    def calculate_stock_cost(self, 
                             exchange: str,
                             quantity: int, 
                             price: float,
                             currency: str = "EUR") -> TradeCosts:
        """
        Berechnet Aktien-Trading-Kosten
        
        Args:
            exchange: Börse (XETRA, NYSE, NASDAQ, LSE, SMART)
            quantity: Anzahl Aktien
            price: Preis pro Aktie
            currency: Währung
        """
        order_value = quantity * price
        
        # Kommission basierend auf Börse
        if exchange in ["XETRA", "IBIS"]:
            commission = max(self.XETRA_MIN, 
                           min(order_value * self.XETRA_RATE, self.XETRA_MAX))
        elif exchange in ["NYSE", "NASDAQ", "ARCA", "SMART"]:
            commission = max(self.US_MIN, quantity * self.US_PER_SHARE)
        elif exchange in ["LSE", "LSEETF"]:
            commission = max(self.UK_MIN, order_value * self.UK_RATE)
        else:
            # Fallback: 0.1%, min 4
            commission = max(4.0, order_value * 0.001)
        
        # Spread schätzen (abhängig von Liquidität)
        if order_value > 100000:
            spread_estimate = order_value * 0.0002  # 0.02% für große Orders
        elif order_value > 10000:
            spread_estimate = order_value * 0.0005  # 0.05% Standard
        else:
            spread_estimate = order_value * 0.001   # 0.1% für kleine Orders
        
        # Gesamtkosten für Roundtrip (Kauf + Verkauf)
        total_roundtrip = (commission * 2) + (spread_estimate * 2)
        
        # Breakeven-Bewegung in Prozent
        breakeven_move = (total_roundtrip / order_value) * 100 if order_value > 0 else float('inf')
        
        return TradeCosts(
            commission=commission,
            spread_estimate=spread_estimate,
            total_roundtrip=total_roundtrip,
            breakeven_move=breakeven_move
        )
    
    def calculate_option_cost(self,
                              exchange: str,
                              contracts: int) -> TradeCosts:
        """Berechnet Optionen-Trading-Kosten"""
        if exchange in ["EUREX", "DTB"]:
            commission = contracts * self.OPTION_DE
        else:  # US Optionen
            commission = contracts * self.OPTION_US
        
        # Optionen haben höhere Spreads
        spread_estimate = contracts * 5.0  # Grobe Schätzung
        
        total_roundtrip = (commission * 2) + (spread_estimate * 2)
        
        return TradeCosts(
            commission=commission,
            spread_estimate=spread_estimate,
            total_roundtrip=total_roundtrip,
            breakeven_move=0  # Bei Optionen anders berechnet
        )
    
    def is_trade_viable(self, 
                        costs: TradeCosts, 
                        expected_return_pct: float,
                        min_reward_ratio: float = 2.0) -> bool:
        """
        Prüft ob ein Trade profitabel sein kann
        
        Args:
            costs: Berechnete Kosten
            expected_return_pct: Erwarteter Return in %
            min_reward_ratio: Minimum Reward/Risk Ratio
        """
        return expected_return_pct > (costs.breakeven_move * min_reward_ratio)


@dataclass
class PositionSizeResult:
    """Ergebnis der Position-Sizing-Berechnung"""
    shares: int                 # Anzahl Aktien
    position_value: float       # Gesamtwert der Position
    risk_amount: float          # Risiko in EUR
    stop_loss_price: float      # Stop-Loss Preis
    take_profit_price: float    # Take-Profit Preis
    viable: bool                # Trade sinnvoll?
    reason: str                 # Begründung


class RiskManager:
    """
    Risikomanagement für Position Sizing und Portfolio-Risiko
    """
    
    def __init__(self,
                 total_capital: float,
                 max_position_pct: float = 0.05,       # Max 5% pro Position
                 max_risk_per_trade_pct: float = 0.01, # Max 1% Risiko pro Trade
                 max_portfolio_risk_pct: float = 0.10, # Max 10% Gesamtrisiko
                 max_correlation: float = 0.7):        # Max Korrelation
        
        self.total_capital = total_capital
        self.max_position_pct = max_position_pct
        self.max_risk_per_trade = max_risk_per_trade_pct
        self.max_portfolio_risk = max_portfolio_risk_pct
        self.max_correlation = max_correlation
        self.cost_calculator = CostCalculator()
    
    def update_capital(self, new_capital: float):
        """Aktualisiert das verfügbare Kapital"""
        self.total_capital = new_capital
    
    def calculate_position_size(self,
                                symbol: str,
                                entry_price: float,
                                strategy: str,
                                signal_strength: float,
                                exchange: str,
                                current_portfolio_risk: float,
                                volatility: Optional[float] = None) -> PositionSizeResult:
        """
        Berechnet optimale Position Size
        
        Args:
            symbol: Ticker-Symbol
            entry_price: Einstiegspreis
            strategy: "momentum" oder "mean_reversion"
            signal_strength: Signalstärke (0-1)
            exchange: Börse
            current_portfolio_risk: Aktuelles Portfolio-Risiko (0-1)
            volatility: Optionale Volatilität für dynamische Stops
        """
        # 1. Portfolio-Risiko-Limit prüfen
        if current_portfolio_risk >= self.max_portfolio_risk:
            return PositionSizeResult(
                shares=0, position_value=0, risk_amount=0,
                stop_loss_price=0, take_profit_price=0,
                viable=False, 
                reason="Portfolio-Risiko-Limit erreicht"
            )
        
        # 2. Stop-Loss und Take-Profit basierend auf Strategie
        if strategy == "momentum":
            stop_loss_pct = volatility * 1.5 if volatility else 0.05   # 5% oder 1.5× Volatilität
            take_profit_pct = stop_loss_pct * 3  # 3:1 Ratio
        elif strategy == "mean_reversion":
            stop_loss_pct = volatility * 1.0 if volatility else 0.03   # 3% oder 1× Volatilität
            take_profit_pct = stop_loss_pct * 2  # 2:1 Ratio
        elif strategy == "hedge":
            stop_loss_pct = 0.07   # Breiter Stop für Hedges
            take_profit_pct = 0.10
        else:
            stop_loss_pct = 0.04
            take_profit_pct = 0.08
        
        stop_loss_price = entry_price * (1 - stop_loss_pct)
        take_profit_price = entry_price * (1 + take_profit_pct)
        risk_per_share = entry_price - stop_loss_price
        
        if risk_per_share <= 0:
            return PositionSizeResult(
                shares=0, position_value=0, risk_amount=0,
                stop_loss_price=stop_loss_price, take_profit_price=take_profit_price,
                viable=False, 
                reason="Ungültiger Stop-Loss"
            )
        
        # 3. Position Size nach Risiko
        max_risk_amount = self.total_capital * self.max_risk_per_trade
        shares_by_risk = int(max_risk_amount / risk_per_share)
        
        # 4. Position Size nach Kapital
        max_position_value = self.total_capital * self.max_position_pct
        shares_by_capital = int(max_position_value / entry_price)
        
        # 5. Signal-Stärke einbeziehen
        base_shares = min(shares_by_risk, shares_by_capital)
        adjusted_shares = int(base_shares * signal_strength)
        
        # Minimum 1 Aktie
        if adjusted_shares < 1:
            return PositionSizeResult(
                shares=0, position_value=0, risk_amount=0,
                stop_loss_price=stop_loss_price, take_profit_price=take_profit_price,
                viable=False, 
                reason="Position zu klein nach Anpassung"
            )
        
        position_value = adjusted_shares * entry_price
        risk_amount = adjusted_shares * risk_per_share
        
        # 6. Gebühren-Check
        costs = self.cost_calculator.calculate_stock_cost(
            exchange=exchange,
            quantity=adjusted_shares,
            price=entry_price
        )
        
        expected_return = take_profit_pct * 100
        if not self.cost_calculator.is_trade_viable(costs, expected_return, min_reward_ratio=2.0):
            return PositionSizeResult(
                shares=0, position_value=0, risk_amount=0,
                stop_loss_price=stop_loss_price, take_profit_price=take_profit_price,
                viable=False, 
                reason=f"Gebühren zu hoch: {costs.breakeven_move:.2f}% Breakeven, {expected_return:.1f}% erwartet"
            )
        
        return PositionSizeResult(
            shares=adjusted_shares,
            position_value=position_value,
            risk_amount=risk_amount,
            stop_loss_price=round(stop_loss_price, 2),
            take_profit_price=round(take_profit_price, 2),
            viable=True,
            reason=f"OK - {adjusted_shares} Stück, Gebühren: {costs.commission:.2f}€, Breakeven: {costs.breakeven_move:.2f}%"
        )
    
    def check_drawdown(self, 
                       current_equity: float, 
                       peak_equity: float,
                       max_drawdown: float = 0.15) -> tuple[bool, float]:
        """
        Prüft ob Drawdown-Limit erreicht ist
        
        Returns:
            (limit_reached, current_drawdown)
        """
        if peak_equity <= 0:
            return False, 0.0
        
        current_drawdown = (peak_equity - current_equity) / peak_equity
        limit_reached = current_drawdown >= max_drawdown
        
        return limit_reached, current_drawdown
    
    def calculate_portfolio_var(self, 
                                positions: list[dict],
                                confidence: float = 0.95) -> float:
        """
        Berechnet Value at Risk für das Portfolio
        Vereinfachte Implementierung
        
        Args:
            positions: Liste von {"value": float, "volatility": float}
            confidence: Konfidenzniveau (z.B. 0.95 für 95%)
        """
        import math
        
        if not positions:
            return 0.0
        
        # Z-Score für Konfidenzniveau
        z_scores = {0.90: 1.28, 0.95: 1.65, 0.99: 2.33}
        z = z_scores.get(confidence, 1.65)
        
        # Vereinfachter VaR (ohne Korrelationsmatrix)
        total_var_squared = 0.0
        for pos in positions:
            value = pos.get("value", 0)
            vol = pos.get("volatility", 0.02)  # Default 2% tägliche Vol
            position_var = value * vol * z
            total_var_squared += position_var ** 2
        
        return math.sqrt(total_var_squared)


# Test
if __name__ == "__main__":
    # Test Cost Calculator
    calc = CostCalculator()
    
    # Xetra Trade
    costs = calc.calculate_stock_cost("XETRA", 100, 50.0)
    print(f"Xetra 100 × 50€:")
    print(f"  Kommission: {costs.commission:.2f}€")
    print(f"  Spread: {costs.spread_estimate:.2f}€")
    print(f"  Roundtrip: {costs.total_roundtrip:.2f}€")
    print(f"  Breakeven: {costs.breakeven_move:.2f}%")
    
    print()
    
    # US Trade
    costs = calc.calculate_stock_cost("NASDAQ", 50, 150.0)
    print(f"NASDAQ 50 × $150:")
    print(f"  Kommission: {costs.commission:.2f}$")
    print(f"  Breakeven: {costs.breakeven_move:.2f}%")
    
    print()
    
    # Risk Manager Test
    rm = RiskManager(total_capital=50000)
    
    sizing = rm.calculate_position_size(
        symbol="AAPL",
        entry_price=150.0,
        strategy="momentum",
        signal_strength=0.8,
        exchange="NASDAQ",
        current_portfolio_risk=0.02
    )
    
    print(f"Position Sizing AAPL @ $150:")
    print(f"  Shares: {sizing.shares}")
    print(f"  Value: ${sizing.position_value:,.2f}")
    print(f"  Risk: ${sizing.risk_amount:,.2f}")
    print(f"  Stop-Loss: ${sizing.stop_loss_price:.2f}")
    print(f"  Take-Profit: ${sizing.take_profit_price:.2f}")
    print(f"  Viable: {sizing.viable}")
    print(f"  Reason: {sizing.reason}")
