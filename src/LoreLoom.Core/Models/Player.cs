namespace LoreLoom.Core.Models;

public class Player
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid? CharacterId { get; set; }
    public required string Name { get; set; }
    public required string Token { get; set; }
    public bool IsCurrentTurn { get; set; }

    public Game Game { get; set; } = null!;
    public Character? Character { get; set; }
    public ICollection<Turn> Turns { get; set; } = [];
    public ICollection<GameResult> GameResults { get; set; } = [];
}
