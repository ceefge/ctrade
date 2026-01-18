using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CTrader.Services.Configuration;

namespace CTrader.Services.Analysis.LlmClients;

public class AnthropicLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly IParameterService _parameters;
    private readonly ILogger<AnthropicLlmClient> _logger;
    private readonly string? _apiKey;

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    public AnthropicLlmClient(HttpClient httpClient, IConfiguration configuration, IParameterService parameters, ILogger<AnthropicLlmClient> logger)
    {
        _httpClient = httpClient;
        _parameters = parameters;
        _logger = logger;
        _apiKey = configuration["Anthropic:ApiKey"];

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Anthropic API key not configured");
            return string.Empty;
        }

        var modelName = await _parameters.GetValueAsync("LLM", "Model", "claude-sonnet-4-20250514");
        var maxTokens = await _parameters.GetValueAsync("LLM", "MaxTokens", 4096);

        try
        {
            var request = new AnthropicRequest
            {
                Model = modelName,
                MaxTokens = maxTokens,
                System = systemPrompt,
                Messages =
                [
                    new AnthropicMessage { Role = "user", Content = userPrompt }
                ]
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(ApiUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<AnthropicResponse>(responseJson);

            return result?.Content?.FirstOrDefault()?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Anthropic API");
            throw;
        }
    }

    public async Task<T?> CompleteJsonAsync<T>(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) where T : class
    {
        var jsonSystemPrompt = systemPrompt + "\n\nRespond ONLY with valid JSON, no additional text or explanation.";
        var response = await CompleteAsync(jsonSystemPrompt, userPrompt, cancellationToken);

        if (string.IsNullOrEmpty(response))
            return null;

        try
        {
            var jsonContent = ExtractJson(response);
            return JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response as JSON: {Response}", response);
            return null;
        }
    }

    private static string ExtractJson(string response)
    {
        var trimmed = response.Trim();

        if (trimmed.StartsWith("```json"))
        {
            var start = trimmed.IndexOf('\n') + 1;
            var end = trimmed.LastIndexOf("```");
            if (end > start)
                return trimmed[start..end].Trim();
        }
        else if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('\n') + 1;
            var end = trimmed.LastIndexOf("```");
            if (end > start)
                return trimmed[start..end].Trim();
        }

        return trimmed;
    }

    private class AnthropicRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("system")]
        public string? System { get; set; }

        [JsonPropertyName("messages")]
        public List<AnthropicMessage> Messages { get; set; } = [];
    }

    private class AnthropicMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContent>? Content { get; set; }
    }

    private class AnthropicContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
