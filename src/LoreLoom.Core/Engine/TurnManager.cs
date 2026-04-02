using LoreLoom.Core.Data;
using LoreLoom.Core.Enums;
using LoreLoom.Core.Localization;
using LoreLoom.Core.Models;
using LoreLoom.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace LoreLoom.Core.Engine;

public class TurnResult
{
    public required string Narrative { get; init; }
    public int ResourceCost { get; init; }
    public int ResourcePctAfter { get; init; }
    public bool Victory { get; init; }
    public bool GameOver { get; init; }
    public string? Postmortem { get; init; }
    public List<XpAward>? XpPerPlayer { get; init; }
}

public class TurnManager(LoreLoomDbContext db, ILlmService llm, IAppTextLocalizer text)
{
    public async Task<TurnResult> ProcessTurnAsync(Guid gameId, Guid playerId, string action, string? culture = null)
    {
        var game = await db.Games
            .FirstOrDefaultAsync(g => g.Id == gameId)
            ?? throw new InvalidOperationException(text["api_game_not_found"]);

        if (game.Status != GameStatus.Active)
            throw new InvalidOperationException(text["api_game_not_active"]);

        var player = await db.Players
            .Include(p => p.Character)
            .FirstOrDefaultAsync(p => p.Id == playerId && p.GameId == gameId)
            ?? throw new InvalidOperationException(text["api_player_not_found"]);

        if (!player.IsCurrentTurn)
            throw new InvalidOperationException(text["api_not_players_turn"]);

        var resolvedCulture = AppCultures.Normalize(culture ?? game.Culture);
        if (!string.Equals(game.Culture, resolvedCulture, StringComparison.Ordinal))
        {
            game.Culture = resolvedCulture;
            game.SystemPrompt = null;
        }

        var players = await db.Players
            .Include(p => p.Character)
            .Where(p => p.GameId == gameId)
            .ToListAsync();

        var allTurns = await db.Turns
            .Where(t => t.GameId == gameId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        // Build LLM request
        var systemPrompt = game.SystemPrompt ?? ContextBuilder.BuildSystemPrompt(game, resolvedCulture);
        if (game.SystemPrompt is null)
        {
            game.SystemPrompt = systemPrompt;
        }

        var messages = ContextBuilder.BuildMessages(game, allTurns, players, action, player.Name);
        var request = new LlmRequest(systemPrompt, messages);

        // Call LLM
        var response = await llm.SendAsync(request);

        // Apply resource cost
        ResourceTracker.ApplyCost(game, response.ResourceCost);

        var gameOver = ResourceTracker.IsGameOver(game);
        var victory = response.Victory;

        if (victory)
            game.Status = GameStatus.Finished;

        // Save the turn
        var turn = new Turn
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            PlayerId = playerId,
            PlayerAction = action,
            DmResponse = response.Narrative,
            ResourceCost = response.ResourceCost
        };
        db.Turns.Add(turn);

        // Advance turn to next player
        AdvanceTurn(players, player);

        // If game ended, save postmortem and create results
        if (gameOver || victory)
        {
            game.Status = GameStatus.Finished;
            game.Postmortem = response.Postmortem;
            await CreateGameResults(game, players, response);
        }

        // Auto-summarize session if needed
        var totalTurns = allTurns.Count + 1; // +1 for the turn we just added
        if (!gameOver && !victory && ContextBuilder.NeedsSummary(totalTurns, game.LastSummaryTurn))
        {
            await GenerateSessionSummary(game, allTurns, players, resolvedCulture);
        }

        await db.SaveChangesAsync();

        return new TurnResult
        {
            Narrative = response.Narrative,
            ResourceCost = response.ResourceCost,
            ResourcePctAfter = game.ResourcePct,
            Victory = victory,
            GameOver = gameOver || victory,
            Postmortem = response.Postmortem,
            XpPerPlayer = response.XpPerPlayer
        };
    }

    private static void AdvanceTurn(List<Player> players, Player current)
    {
        if (players.Count <= 1) return;

        var ordered = players.OrderBy(p => p.Id).ToList();
        var currentIndex = ordered.FindIndex(p => p.Id == current.Id);
        var nextIndex = (currentIndex + 1) % ordered.Count;

        current.IsCurrentTurn = false;
        ordered[nextIndex].IsCurrentTurn = true;
    }

    private async Task GenerateSessionSummary(Game game, List<Turn> allTurns, List<Player> players, string language)
    {
        try
        {
            var turnsToSummarize = allTurns
                .OrderBy(t => t.CreatedAt)
                .Skip(game.LastSummaryTurn)
                .ToList();

            var summaryRequest = ContextBuilder.BuildSummaryRequest(game, turnsToSummarize, players, language);
            var summaryResponse = await llm.SendAsync(summaryRequest);

            game.SessionSummary = summaryResponse.Narrative;
            game.LastSummaryTurn = allTurns.Count;
        }
        catch
        {
            // Summary generation is non-critical; if it fails, we continue without it
        }
    }

    private async Task CreateGameResults(Game game, List<Player> players, LlmResponse response)
    {
        foreach (var player in players)
        {
            var xpAward = response.XpPerPlayer?.FirstOrDefault(x =>
                x.PlayerName.Equals(player.Name, StringComparison.OrdinalIgnoreCase));

            var xpEarned = xpAward?.Xp ?? 50;

            var result = new GameResult
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                PlayerId = player.Id,
                CharacterId = player.CharacterId,
                XpEarned = xpEarned,
                PointsEarned = response.Victory ? xpEarned : xpEarned / 2
            };
            db.GameResults.Add(result);

            // Apply XP to character if linked
            if (player.CharacterId.HasValue)
            {
                var character = await db.Characters.FindAsync(player.CharacterId.Value);
                if (character is not null)
                {
                    character.Xp += xpEarned;
                    while (character.Xp >= character.Level * 100)
                    {
                        character.Xp -= character.Level * 100;
                        character.Level++;
                    }
                    character.LastPlayedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
