using LoreLoom.Core.Models;
using LoreLoom.Core.Services;

namespace LoreLoom.Core.Engine;

public static class ContextBuilder
{
    private const int SlidingWindowSize = 10;
    public const int SummaryInterval = 15;

    public static string BuildSystemPrompt(Game game, string language)
    {
        return $$"""
            You are the Dungeon Master of a tabletop RPG game.

            SETTING: {{game.Setting}}
            GAME TITLE: {{game.Title}}

            RULES:
            - Each player has three stats: Strength (physical), Wit (mental/cunning), Charisma (social). Values range 1-5.
            - When an action requires a check, you internally simulate a d6 + relevant stat.
              - Total >= 7: full success. 5-6: partial success. <= 4: failure.
            - Describe outcomes narratively. Never mention dice numbers to players.
            - Players do NOT have hit points in this system.

            RESOURCE MECHANIC:
            - The party has a shared resource called "{{game.ResourceName}}" currently at {{game.ResourcePct}}%.
            - This resource decreases each turn based on the action's risk:
              - Low risk (talking, exploring): cost 2-5
              - Normal actions: cost 6-10
              - Risky actions (combat, magic): cost 11-20
              - Brilliant / exceptional success: NEGATIVE cost (resource recovery)
            - When the resource reaches 0%, the game ends in narrative defeat.
            - You may declare victory (victory: true) when the story reaches a satisfying climax.

            RESPONSE FORMAT:
            You MUST respond ONLY with valid JSON, no text outside of it:
            {
              "narrative": "Your narration here...",
              "resource_cost": <integer>,
              "victory": <boolean>
            }

            When victory is true OR resource drops to 0, also include:
            {
              "narrative": "...",
              "resource_cost": <integer>,
              "victory": <boolean>,
              "postmortem": "Epic summary of the adventure...",
              "xp_per_player": [
                {"player_name": "Name", "xp": <integer>, "reason": "Why they earned this XP"}
              ]
            }

            LANGUAGE: Respond in {{language}}. All narrative text must be in {{language}}.
            """;
    }

    public static List<LlmMessage> BuildMessages(Game game, List<Turn> allTurns, List<Player> players, string currentAction, string currentPlayerName)
    {
        var messages = new List<LlmMessage>();

        // Campaign context (first message)
        var playerDescriptions = string.Join("\n", players.Select(p =>
        {
            if (p.Character is not null)
            {
                var c = p.Character;
                return $"- {p.Name}: Strength {c.Strength}, Wit {c.Wit}, Charisma {c.Charisma} (Level {c.Level}). {c.Backstory ?? "No backstory."}";
            }
            return $"- {p.Name}: No character sheet.";
        }));

        messages.Add(new LlmMessage("user", $"""
            CAMPAIGN START
            Setting: {game.Setting}
            Resource: {game.ResourceName} (a vital force that diminishes as the adventure progresses)

            Players:
            {playerDescriptions}

            Begin the adventure!
            """));

        // Session summary (replaces old turns when available)
        if (!string.IsNullOrWhiteSpace(game.SessionSummary))
        {
            messages.Add(new LlmMessage("user", $"SESSION SUMMARY (turns 1-{game.LastSummaryTurn}):\n{game.SessionSummary}"));
            messages.Add(new LlmMessage("assistant", """{"narrative": "Understood. I have the session context. Continuing the adventure.", "resource_cost": 0, "victory": false}"""));
        }

        // Sliding window of recent turns (after the summary)
        var turnsAfterSummary = allTurns
            .OrderBy(t => t.CreatedAt)
            .Skip(game.LastSummaryTurn)
            .TakeLast(SlidingWindowSize)
            .ToList();

        foreach (var turn in turnsAfterSummary)
        {
            var playerName = players.FirstOrDefault(p => p.Id == turn.PlayerId)?.Name ?? "Unknown";
            messages.Add(new LlmMessage("user", $"[{playerName}] does: {turn.PlayerAction}"));
            messages.Add(new LlmMessage("assistant", turn.DmResponse));
        }

        // Current turn
        var presentPlayers = string.Join(", ", players.Select(p => p.Name));
        messages.Add(new LlmMessage("user", $"""
            [{currentPlayerName}] does: {currentAction}
            Current resource ({game.ResourceName}): {game.ResourcePct}%
            Players present: {presentPlayers}
            """));

        return messages;
    }

    public static LlmRequest BuildSummaryRequest(Game game, List<Turn> turns, List<Player> players, string language)
    {
        var systemPrompt = $"""
            You are a session summarizer for a tabletop RPG game.
            Summarize the adventure so far in a concise but complete way.
            Include key events, decisions, NPC interactions, and current narrative state.
            Respond ONLY with the summary text, no JSON, no extra formatting.
            Write in {language}.
            """;

        var messages = new List<LlmMessage>();

        var turnLog = string.Join("\n\n", turns.Select(t =>
        {
            var playerName = players.FirstOrDefault(p => p.Id == t.PlayerId)?.Name ?? "Unknown";
            return $"[{playerName}]: {t.PlayerAction}\nDM: {t.DmResponse}";
        }));

        var existingSummary = !string.IsNullOrWhiteSpace(game.SessionSummary)
            ? $"PREVIOUS SUMMARY:\n{game.SessionSummary}\n\nNEW TURNS TO INCORPORATE:\n"
            : "";

        messages.Add(new LlmMessage("user", $"""
            Game: {game.Title}
            Setting: {game.Setting}
            Resource: {game.ResourceName} at {game.ResourcePct}%

            {existingSummary}{turnLog}

            Provide a comprehensive summary of everything that has happened.
            """));

        return new LlmRequest(systemPrompt, messages);
    }

    public static bool NeedsSummary(int totalTurns, int lastSummaryTurn)
    {
        return totalTurns - lastSummaryTurn >= SummaryInterval;
    }
}
