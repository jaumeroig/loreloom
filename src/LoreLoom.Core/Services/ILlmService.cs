namespace LoreLoom.Core.Services;

public interface ILlmService
{
    Task<LlmResponse> SendAsync(LlmRequest request);
}

public record LlmRequest(
    string SystemPrompt,
    List<LlmMessage> Messages
);

public record LlmMessage(string Role, string Content);

public record LlmResponse(
    string Narrative,
    int ResourceCost,
    bool Victory,
    string? Postmortem,
    List<XpAward>? XpPerPlayer
);

public record XpAward(string PlayerName, int Xp, string Reason);
