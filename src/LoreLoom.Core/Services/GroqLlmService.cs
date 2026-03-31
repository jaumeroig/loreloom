using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoreLoom.Core.Services;

public class GroqOptions
{
    public const string SectionName = "Groq";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "llama-3.3-70b-versatile";
}

public class GroqLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly GroqOptions _options;
    private readonly ILogger<GroqLlmService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GroqLlmService(HttpClient httpClient, IOptions<GroqOptions> options, ILogger<GroqLlmService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmResponse> SendAsync(LlmRequest request)
    {
        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };

        foreach (var msg in request.Messages)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        var body = new
        {
            model = _options.Model,
            messages,
            temperature = 0.8,
            max_tokens = 2048,
            response_format = new { type = "json_object" }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
        httpRequest.Content = JsonContent.Create(body);

        using var httpResponse = await _httpClient.SendAsync(httpRequest);
        httpResponse.EnsureSuccessStatusCode();

        var groqResponse = await httpResponse.Content.ReadFromJsonAsync<GroqChatResponse>();
        var content = groqResponse?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Empty response from Groq API");
            return new LlmResponse("...", 7, false, null, null);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<LlmJsonResponse>(content, JsonOptions);
            if (parsed is null)
                return new LlmResponse("...", 7, false, null, null);

            return new LlmResponse(
                parsed.Narrative ?? "...",
                parsed.ResourceCost ?? 7,
                parsed.Victory ?? false,
                parsed.Postmortem,
                parsed.XpPerPlayer?.Select(x => new XpAward(x.PlayerName ?? "", x.Xp ?? 0, x.Reason ?? "")).ToList()
            );
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response JSON: {Content}", content);
            return new LlmResponse(content, 7, false, null, null);
        }
    }

    private record GroqChatResponse(List<GroqChoice>? Choices);
    private record GroqChoice(GroqMessage? Message);
    private record GroqMessage(string? Content);

    private class LlmJsonResponse
    {
        public string? Narrative { get; set; }
        public int? ResourceCost { get; set; }
        public bool? Victory { get; set; }
        public string? Postmortem { get; set; }
        public List<XpAwardJson>? XpPerPlayer { get; set; }
    }

    private class XpAwardJson
    {
        public string? PlayerName { get; set; }
        public int? Xp { get; set; }
        public string? Reason { get; set; }
    }
}
