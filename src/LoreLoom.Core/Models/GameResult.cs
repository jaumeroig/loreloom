namespace LoreLoom.Core.Models;

public class GameResult
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid? CharacterId { get; set; }
    public int XpEarned { get; set; }
    public int PointsEarned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Game Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public Character? Character { get; set; }
}
