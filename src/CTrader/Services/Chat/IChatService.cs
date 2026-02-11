namespace CTrader.Services.Chat;

public interface IChatService
{
    IReadOnlyList<ChatMessage> Messages { get; }
    Task SendMessageAsync(string userMessage, CancellationToken cancellationToken = default);
    void ClearHistory();
}
