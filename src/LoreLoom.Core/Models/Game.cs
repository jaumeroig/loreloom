using LoreLoom.Core.Enums;

namespace LoreLoom.Core.Models;

public class Game
{
    public Guid Id { get; set; }
    public required string CreatorToken { get; set; }
    public required string Title { get; set; }
    public required string Setting { get; set; }
    public string Culture { get; set; } = Localization.AppCultures.DefaultCulture;
    public string? SystemPrompt { get; set; }
    public required string ResourceName { get; set; }
    public int ResourcePct { get; set; } = 100;
    public bool IsPublic { get; set; } = true;
    public string? InviteCode { get; set; }
    public int MaxPlayers { get; set; } = 4;
    public GameStatus Status { get; set; } = GameStatus.Waiting;
    public string? SessionSummary { get; set; }
    public int LastSummaryTurn { get; set; }
    public string? Postmortem { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Player> Players { get; set; } = [];
    public ICollection<Turn> Turns { get; set; } = [];
    public ICollection<GameResult> Results { get; set; } = [];
}
