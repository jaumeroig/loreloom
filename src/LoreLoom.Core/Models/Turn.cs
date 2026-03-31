namespace LoreLoom.Core.Models;

public class Turn
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public required string PlayerAction { get; set; }
    public required string DmResponse { get; set; }
    public int ResourceCost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Game Game { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
