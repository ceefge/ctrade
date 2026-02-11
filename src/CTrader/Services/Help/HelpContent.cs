namespace CTrader.Services.Help;

public static class HelpContent
{
    public static readonly HelpSection[] Sections =
    [
        new("dashboard", "Dashboard", "Das Dashboard ist die Startseite von CTrader und gibt einen schnellen Überblick über den aktuellen Zustand des Trading-Systems.",
        [
            ("Trading Service Status", "Zeigt an, ob der Trading-Service läuft (grün = Running, grau = Stopped). Der Service verbindet sich automatisch beim Start mit dem Broker."),
            ("Broker-Verbindung", "Zeigt den Verbindungsstatus zum Interactive Brokers Gateway. 'Connected' (grün) bedeutet, dass Echtzeit-Daten und Order-Ausführung verfügbar sind. 'Disconnected' (rot) bedeutet, dass keine Verbindung besteht — prüfen Sie die IB Gateway Konfiguration."),
            ("Marktregime", "Das aktuelle Marktregime wird durch KI-Analyse ermittelt. Mögliche Regime: RiskOn (bullisch), RiskOff (defensiv), Neutral, HighVolatility (hohe Schwankungen), Crisis (Krisenmodus). Dazu werden Konfidenz, Risikolevel und empfohlene Strategie angezeigt."),
            ("Analyse starten", "Mit 'Run Analysis Now' wird eine sofortige Marktregime-Analyse ausgelöst. Diese nutzt aktuelle Nachrichten und Marktdaten, um das Regime zu bestimmen."),
            ("Letzte Nachrichten", "Zeigt die 10 neuesten Nachrichten mit Quelle, Zeitpunkt und optionalem Sentiment-Score. Mit 'Refresh' werden neue Nachrichten von allen konfigurierten Quellen abgerufen.")
        ]),

        new("configuration", "Konfiguration", "Auf der Konfigurationsseite werden alle wichtigen Einstellungen der App verwaltet. Änderungen werden in der Datenbank gespeichert und bei Bedarf in eine .env-Datei für Docker geschrieben.",
        [
            ("API Keys", "Hier werden die Schlüssel für externe Dienste eingetragen:\n- **Anthropic API Key**: Für die KI-gestützte Marktanalyse und den Chat-Assistenten (Claude). Beginnt mit 'sk-ant-'.\n- **Finnhub API Key**: Für Echtzeit-Marktnachrichten. Kostenlos registrierbar auf finnhub.io.\n- **AlphaVantage API Key**: Für zusätzliche Finanzdaten und Nachrichten."),
            ("IB Gateway", "Einstellungen für die Interactive Brokers Verbindung:\n- **Host**: Hostname des IB Gateway Containers (Standard: 'ib-gateway' in Docker, 'localhost' lokal).\n- **Port**: API-Port des IB Gateway (Standard: 4001 für CapTrader).\n- **Client ID**: Eindeutige ID für die API-Verbindung (Standard: 1)."),
            ("Docker & VNC", "Zugangsdaten für den IB Gateway Docker-Container:\n- **IB Username/Password**: CapTrader-Zugangsdaten für den automatischen Login.\n- **VNC**: Der IB Gateway ist per VNC erreichbar (Port 6081 über noVNC im Browser), um die GUI zu sehen und bei Bedarf manuell einzugreifen."),
            ("Parameter-Verwaltung", "Alle Parameter werden als Schlüssel-Wert-Paare in der SQLite-Datenbank gespeichert. Kategorien: ApiKeys, IbGateway, Docker. Änderungen werden sofort wirksam.")
        ]),

        new("positions", "Positionen", "Die Positionsseite zeigt alle aktuell offenen Positionen und ermöglicht die Verwaltung von Risiko-Parametern.",
        [
            ("Offene Positionen", "Listet alle Positionen mit Symbol, Richtung (Long/Short), Einstiegskurs, aktuellem Kurs, Stückzahl und unrealisiertem Gewinn/Verlust auf."),
            ("Stop-Loss / Take-Profit", "Für jede Position können Stop-Loss und Take-Profit Levels angezeigt und bearbeitet werden. Diese werden vom Risikomanagement automatisch berechnet, können aber manuell angepasst werden."),
            ("Risiko-Berechnung", "Zeigt das Risiko pro Position (in Prozent des Portfolios) und die berechneten Kosten (Spread, Kommission) an. Der CostCalculator berücksichtigt die aktuellen Marktbedingungen."),
            ("Position schließen", "Positionen können manuell geschlossen werden. Der Schließ-Auftrag wird über den Broker ausgeführt. Hinweis: Trading muss aktiviert sein (TradingEnabled=true).")
        ]),

        new("trades", "Trade-Historie", "Die Trade-Historie zeigt alle abgeschlossenen Trades mit Details und Filteroptionen.",
        [
            ("Übersicht", "Alle Trades werden chronologisch aufgelistet mit: Symbol, Richtung, Ein-/Ausstiegskurs, Gewinn/Verlust, Datum und Handelskosten."),
            ("Filter", "Trades können nach Symbol und Zeitraum gefiltert werden. Nutzen Sie die Filter, um die Performance für bestimmte Instrumente oder Zeiträume zu analysieren."),
            ("Zusammenfassung", "Am oberen Rand wird eine Zusammenfassung angezeigt: Gesamtanzahl Trades, Gewinn-/Verlust-Bilanz, Trefferquote und durchschnittlicher Gewinn/Verlust pro Trade."),
            ("Spalten", "Die Tabelle zeigt: Datum, Symbol, Richtung (Buy/Sell), Menge, Einstiegskurs, Ausstiegskurs, Gewinn/Verlust (absolut und in Prozent), Kosten und Netto-Ergebnis.")
        ]),

        new("news", "Nachrichten", "Die Nachrichtenseite aggregiert Finanznachrichten aus mehreren Quellen und bietet KI-gestützte Zusammenfassungen.",
        [
            ("News-Quellen", "CTrader bezieht Nachrichten aus:\n- **Finnhub**: Echtzeit-Marktnachrichten über die Finnhub API.\n- **RSS-Feeds**: Konfigurierbare RSS-Feeds von Finanzportalen.\n- **AlphaVantage**: Zusätzliche Nachrichten und Sentiment-Daten (API Key erforderlich)."),
            ("KI-Zusammenfassung", "Mit dem Button 'AI Summary' wird eine KI-Zusammenfassung der aktuellen Nachrichtenlage erstellt. Die Zusammenfassung analysiert die wichtigsten Themen, Trends und mögliche Auswirkungen auf den Markt."),
            ("Sentiment-Bewertung", "Nachrichten werden automatisch mit einem Sentiment-Score versehen: positiv (grün, > 0.2), neutral (grau) oder negativ (rot, < -0.2). Dies hilft bei der schnellen Einschätzung der Nachrichtenlage."),
            ("Statistiken", "Die Nachrichtenseite zeigt Statistiken wie: Anzahl Nachrichten pro Quelle, Sentiment-Verteilung und Nachrichtenfrequenz über die Zeit.")
        ]),

        new("logs", "System Logs", "Die Log-Seite bietet Einblick in die Systemaktivitäten und Analyse-Ergebnisse.",
        [
            ("Aktivitäts-Logs", "Alle wichtigen Systemereignisse werden protokolliert: Broker-Verbindungen, Trade-Ausführungen, Analyse-Ergebnisse, Fehler und Warnungen. Jeder Eintrag enthält Zeitstempel, Kategorie und Beschreibung."),
            ("Regime-Analyse-Verlauf", "Zeigt die Historie aller durchgeführten Marktregime-Analysen mit Ergebnis, Konfidenz und Zeitstempel. Nützlich, um Regime-Wechsel über die Zeit nachzuvollziehen."),
            ("System-Info", "Zeigt Informationen über den Systemzustand: Datenbankgröße, letzte Analyse, Broker-Verbindungsstatus und Log-Dateien.")
        ]),

        new("monitor", "Aktivitäts-Monitor", "Der Monitor zeigt Echtzeit-Aktivitäten des Systems und ermöglicht die Überwachung aller Vorgänge.",
        [
            ("Auto-Refresh", "Der Monitor aktualisiert sich automatisch in konfigurierbaren Intervallen. So können Sie das System in Echtzeit überwachen, ohne manuell zu aktualisieren."),
            ("Filter", "Aktivitäten können nach Typ gefiltert werden: Trades, Analysen, Verbindungen, Fehler. Dies hilft, schnell die relevanten Einträge zu finden."),
            ("Detail-Ansicht", "Jeder Eintrag kann aufgeklappt werden, um detaillierte Informationen zu sehen: vollständige Fehlermeldungen, Analyse-Details oder Trade-Parameter."),
            ("Logs bereinigen", "Alte Log-Einträge können über den Monitor bereinigt werden, um die Datenbank schlank zu halten. Es wird empfohlen, regelmäßig Logs älter als 30 Tage zu löschen.")
        ]),

        new("general", "Allgemein", "CTrader ist eine Blazor Server Trading-Anwendung, die KI-gestützte Marktanalyse mit automatisiertem Trading über Interactive Brokers verbindet.",
        [
            ("App-Überblick", "CTrader verbindet mehrere Komponenten:\n- **Blazor Server UI**: Echtzeit-Web-Interface mit 7 Hauptseiten.\n- **Interactive Brokers Gateway**: Broker-Anbindung für Marktdaten und Order-Ausführung.\n- **KI-Analyse**: Claude (Anthropic) analysiert Nachrichten und bestimmt das Marktregime.\n- **Risikomanagement**: Automatische Positionsgrößen-Berechnung und Stop-Loss-Verwaltung.\n- **SQLite Datenbank**: Lokale Speicherung aller Daten, Konfiguration und Logs."),
            ("Marktregime", "Das Marktregime-System ist das Herzstück von CTrader. Es klassifiziert den Markt in 5 Zustände:\n- **RiskOn**: Bullischer Markt, offensive Strategien.\n- **RiskOff**: Defensiver Markt, reduzierte Positionen.\n- **Neutral**: Seitwärtsmarkt, neutrale Positionierung.\n- **HighVolatility**: Hohe Schwankungen, angepasste Stops.\n- **Crisis**: Krisenmodus, minimale Exposition."),
            ("Setup-Tipps", "1. API Keys konfigurieren (mindestens Anthropic für KI-Analyse).\n2. IB Gateway starten (Docker Compose oder manuell).\n3. VNC-Verbindung prüfen und bei Bedarf manuell einloggen.\n4. Erste Analyse auf dem Dashboard starten.\n5. Nachrichten-Quellen prüfen.\n6. Trading erst aktivieren, wenn alles funktioniert (TradingEnabled=true)."),
            ("KI-Features", "CTrader nutzt KI (Claude von Anthropic) für:\n- **Marktregime-Analyse**: Automatische Klassifizierung der Marktlage.\n- **Nachrichten-Zusammenfassung**: KI-generierte Zusammenfassungen aktueller Nachrichten.\n- **Chat-Assistent**: Ein interaktiver Assistent, der alle Fragen zur App beantwortet (dieses Feature).\n\nAlle KI-Features benötigen einen gültigen Anthropic API Key.")
        ])
    ];

    public static string GetFullHelpText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# CTrader Hilfe-Dokumentation");
        sb.AppendLine();

        foreach (var section in Sections)
        {
            sb.AppendLine($"## {section.Title}");
            sb.AppendLine(section.Description);
            sb.AppendLine();

            foreach (var (topic, content) in section.Topics)
            {
                sb.AppendLine($"### {topic}");
                sb.AppendLine(content);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}

public record HelpSection(string Id, string Title, string Description, (string Title, string Content)[] Topics);
