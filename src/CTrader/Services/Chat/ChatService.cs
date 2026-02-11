using CTrader.Services.Analysis.LlmClients;
using CTrader.Services.Help;
using Microsoft.Extensions.Logging;

namespace CTrader.Services.Chat;

public class ChatService : IChatService
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<ChatService> _logger;
    private readonly List<ChatMessage> _messages = [];
    private const int MaxHistoryMessages = 20;

    private static readonly string SystemPrompt = $"""
        Du bist der CTrader-Assistent, ein hilfreicher deutschsprachiger KI-Assistent für die CTrader Trading-Anwendung.

        Deine Aufgaben:
        - Beantworte Fragen zur Bedienung und Funktionsweise von CTrader.
        - Erkläre Features, Konfiguration und Konzepte der App.
        - Hilf bei Problemen und gib Tipps zur optimalen Nutzung.
        - Antworte immer auf Deutsch, klar und prägnant.
        - Beziehe dich auf die unten stehende Dokumentation.
        - Wenn du etwas nicht weißt, sage es ehrlich.
        - Gib keine Anlageberatung oder Trading-Empfehlungen.

        Hier ist die vollständige Dokumentation der App:

        {HelpContent.GetFullHelpText()}
        """;

    public ChatService(ILlmClient llmClient, ILogger<ChatService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

    public async Task SendMessageAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        var userMsg = new ChatMessage { Role = "user", Content = userMessage };
        _messages.Add(userMsg);

        try
        {
            var userPrompt = BuildConversationPrompt();
            var response = await _llmClient.CompleteAsync(SystemPrompt, userPrompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                _messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = "Der Anthropic API Key ist nicht konfiguriert. Bitte tragen Sie ihn auf der Konfigurationsseite ein."
                });
                return;
            }

            var assistantMsg = new ChatMessage { Role = "assistant", Content = response };
            _messages.Add(assistantMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat completion failed");
            var errorMsg = new ChatMessage
            {
                Role = "assistant",
                Content = "Entschuldigung, es ist ein Fehler aufgetreten. Bitte prüfen Sie, ob der Anthropic API Key korrekt konfiguriert ist, und versuchen Sie es erneut."
            };
            _messages.Add(errorMsg);
        }

        TrimHistory();
    }

    public void ClearHistory()
    {
        _messages.Clear();
    }

    private string BuildConversationPrompt()
    {
        var sb = new System.Text.StringBuilder();

        foreach (var msg in _messages)
        {
            var role = msg.Role == "user" ? "Benutzer" : "Assistent";
            sb.AppendLine($"{role}: {msg.Content}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void TrimHistory()
    {
        while (_messages.Count > MaxHistoryMessages)
        {
            _messages.RemoveAt(0);
        }
    }
}
