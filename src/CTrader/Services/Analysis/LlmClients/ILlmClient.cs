namespace CTrader.Services.Analysis.LlmClients;

public interface ILlmClient
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    Task<T?> CompleteJsonAsync<T>(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) where T : class;
}
