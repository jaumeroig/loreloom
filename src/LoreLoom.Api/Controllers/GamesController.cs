using LoreLoom.Api.Extensions;
using LoreLoom.Core.Data;
using LoreLoom.Core.Dtos;
using LoreLoom.Core.Engine;
using LoreLoom.Core.Enums;
using LoreLoom.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoreLoom.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class GamesController(LoreLoomDbContext db, TurnManager turnManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GameResponse>>> List(
        [FromQuery] GameStatus? status,
        [FromQuery] bool? isPublic)
    {
        var accountToken = this.GetAccountToken();
        var query = db.Games.Include(g => g.Players).AsQueryable();

        if (status.HasValue)
            query = query.Where(g => g.Status == status.Value);
        if (isPublic.HasValue)
            query = query.Where(g => g.IsPublic == isPublic.Value);

        var games = await query
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return games.Select(g => ToResponse(g, currentAccountToken: accountToken)).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GameResponse>> Get(Guid id)
    {
        var accountToken = this.GetAccountToken();
        var game = await db.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game is null) return NotFound();
        return ToResponse(game, currentAccountToken: accountToken);
    }

    [HttpPost]
    public async Task<ActionResult<GameResponse>> Create(CreateGameRequest request)
    {
        var token = this.GetAccountToken() ?? request.CreatorToken;
        var name = this.GetDisplayName() ?? request.CreatorName;

        var game = new Game
        {
            Id = Guid.NewGuid(),
            CreatorToken = token,
            Title = request.Title,
            Setting = request.Setting,
            ResourceName = request.ResourceName,
            IsPublic = request.IsPublic,
            MaxPlayers = request.MaxPlayers,
            InviteCode = request.IsPublic ? null : GenerateInviteCode()
        };

        var player = new Player
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = name,
            Token = token,
            CharacterId = request.CharacterId,
            IsCurrentTurn = true
        };

        db.Games.Add(game);
        db.Players.Add(player);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = game.Id }, ToResponse(game, [player], token));
    }

    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<GameResponse>> Join(Guid id, JoinGameRequest request)
    {
        var game = await db.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game is null) return NotFound();

        if (game.Status != GameStatus.Waiting)
            return BadRequest("Game has already started or finished.");

        if (game.Players.Count >= game.MaxPlayers)
            return BadRequest("Game is full.");

        if (!game.IsPublic && game.InviteCode != request.InviteCode)
            return BadRequest("Invalid invite code.");

        var token = this.GetAccountToken() ?? request.Token;
        var name = this.GetDisplayName() ?? request.Name;

        if (game.Players.Any(p => p.Token == token))
            return BadRequest("You are already in this game.");

        var player = new Player
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = name,
            Token = token,
            CharacterId = request.CharacterId
        };

        db.Players.Add(player);
        await db.SaveChangesAsync();

        return ToResponse(game, currentAccountToken: token);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<GameResponse>> Start(Guid id, StartGameRequest request)
    {
        var game = await db.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game is null) return NotFound();

        if (game.Status != GameStatus.Waiting)
            return BadRequest("Game has already started or finished.");

        var token = this.GetAccountToken() ?? request.Token;
        if (string.IsNullOrWhiteSpace(game.CreatorToken))
            return BadRequest("Unable to verify game ownership for this game.");

        if (game.CreatorToken != token)
            return BadRequest("Only the game creator can start the game.");

        game.Status = GameStatus.Active;
        game.SystemPrompt = ContextBuilder.BuildSystemPrompt(game, request.Language);

        await db.SaveChangesAsync();

        return ToResponse(game, currentAccountToken: token);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var token = this.GetAccountToken();
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized("You must be logged in to delete games.");

        var game = await db.Games.FindAsync(id);
        if (game is null) return NotFound();

        if (string.IsNullOrWhiteSpace(game.CreatorToken))
            return BadRequest("Unable to verify game ownership for this game.");

        if (game.CreatorToken != token)
            return BadRequest("Only the game creator can delete the game.");

        db.Games.Remove(game);
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/turns")]
    public async Task<ActionResult<TurnResultResponse>> SendTurn(Guid id, SendTurnRequest request)
    {
        var game = await db.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game is null) return NotFound();

        var token = this.GetAccountToken() ?? request.Token;
        var player = game.Players.FirstOrDefault(p => p.Token == token);
        if (player is null)
            return BadRequest("You are not in this game.");

        try
        {
            var result = await turnManager.ProcessTurnAsync(id, player.Id, request.Action);

            return new TurnResultResponse(
                result.Narrative,
                result.ResourceCost,
                result.ResourcePctAfter,
                result.Victory,
                result.GameOver,
                result.Postmortem,
                result.XpPerPlayer?.Select(x => new XpAwardResponse(x.PlayerName, x.Xp, x.Reason)).ToList()
            );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}/turns")]
    public async Task<ActionResult<List<TurnResponse>>> GetTurns(Guid id)
    {
        var game = await db.Games.FindAsync(id);
        if (game is null) return NotFound();

        var turns = await db.Turns
            .Include(t => t.Player)
            .Where(t => t.GameId == id)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        return turns.Select(t => new TurnResponse(
            t.Id,
            t.Player.Name,
            t.PlayerAction,
            t.DmResponse,
            t.ResourceCost,
            t.CreatedAt
        )).ToList();
    }

    [HttpGet("{id:guid}/result")]
    public async Task<ActionResult<GameResultResponse>> GetResult(Guid id)
    {
        var game = await db.Games
            .Include(g => g.Results).ThenInclude(r => r.Player)
            .Include(g => g.Results).ThenInclude(r => r.Character)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game is null) return NotFound();

        if (game.Status != GameStatus.Finished)
            return BadRequest("Game has not finished yet.");

        var lastTurn = await db.Turns
            .Where(t => t.GameId == id)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        var victory = game.ResourcePct > 0;

        return new GameResultResponse(
            game.Id,
            game.Title,
            victory,
            game.Postmortem ?? lastTurn?.DmResponse,
            game.Results.Select(r => new PlayerResultResponse(
                r.Player.Name,
                r.XpEarned,
                r.PointsEarned,
                r.Character?.Name,
                r.Character?.Level
            )).ToList()
        );
    }

    [HttpGet("{id:guid}/export")]
    public async Task<ActionResult> Export(Guid id)
    {
        var game = await db.Games
            .Include(g => g.Players).ThenInclude(p => p.Character)
            .Include(g => g.Turns.OrderBy(t => t.CreatedAt)).ThenInclude(t => t.Player)
            .Include(g => g.Results).ThenInclude(r => r.Player)
            .Include(g => g.Results).ThenInclude(r => r.Character)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game is null) return NotFound();

        if (game.Status != GameStatus.Finished)
            return BadRequest("Game has not finished yet.");

        var victory = game.ResourcePct > 0;
        var md = BuildMarkdownExport(game, victory);

        return Content(md, "text/markdown; charset=utf-8");
    }

    private static string BuildMarkdownExport(Game game, bool victory)
    {
        var lines = new List<string>
        {
            $"# {game.Title}",
            $"*Setting: {game.Setting}*",
            $"*Date: {game.CreatedAt:yyyy-MM-dd}*",
            $"*Result: {(victory ? "Victory" : "Defeat")}*",
            "",
            "## Characters",
            "| Player | Character | Level | XP earned |",
            "|--------|-----------|-------|-----------|"
        };

        foreach (var result in game.Results)
        {
            var charName = result.Character?.Name ?? "—";
            var charLevel = result.Character?.Level.ToString() ?? "—";
            lines.Add($"| {result.Player.Name} | {charName} | {charLevel} | {result.XpEarned} |");
        }

        lines.Add("");
        lines.Add("## Chronicle");
        lines.Add("");

        foreach (var turn in game.Turns)
        {
            lines.Add($"**{turn.Player.Name}:** {turn.PlayerAction}");
            lines.Add("");
            lines.Add($"**DM:** {turn.DmResponse}");
            lines.Add("");
            lines.Add("---");
            lines.Add("");
        }

        // Epilogue: use stored postmortem or fallback to last DM response
        var epilogue = game.Postmortem ?? game.Turns.LastOrDefault()?.DmResponse;
        if (!string.IsNullOrWhiteSpace(epilogue))
        {
            lines.Add("## Epilogue");
            lines.Add(epilogue);
        }

        return string.Join("\n", lines);
    }

    [HttpGet("/ranking")]
    public async Task<ActionResult<List<RankingEntry>>> Ranking()
    {
        var results = await db.GameResults
            .Include(r => r.Player)
            .Include(r => r.Character)
            .ToListAsync();

        var ranking = results
            .GroupBy(r => r.Player.Name)
            .Select(g => new
            {
                PlayerName = g.Key,
                CharacterName = g.OrderByDescending(r => r.CreatedAt).First().Character?.Name,
                TotalPoints = g.Sum(r => r.PointsEarned),
                GamesPlayed = g.Select(r => r.GameId).Distinct().Count()
            })
            .OrderByDescending(r => r.TotalPoints)
            .ToList();

        return ranking.Select((r, i) => new RankingEntry(
            i + 1,
            r.PlayerName,
            r.CharacterName,
            r.TotalPoints,
            r.GamesPlayed
        )).ToList();
    }

    private static GameResponse ToResponse(Game g, List<Player>? players = null, string? currentAccountToken = null)
    {
        var p = players ?? g.Players.ToList();
        return new GameResponse(
            g.Id,
            g.Title,
            g.Setting,
            g.ResourceName,
            g.ResourcePct,
            g.IsPublic,
            g.InviteCode,
            g.MaxPlayers,
            g.Status,
            p.Count,
            currentAccountToken is not null && g.CreatorToken == currentAccountToken,
            p.Select(pl => new PlayerResponse(pl.Id, pl.Name, pl.IsCurrentTurn, g.CreatorToken == pl.Token, pl.CharacterId)).ToList(),
            g.CreatedAt
        );
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Random.Shared.GetItems<char>(chars, 6));
    }
}
