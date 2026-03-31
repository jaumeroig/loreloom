namespace LoreLoom.Core.Models;

public class Character
{
    public Guid Id { get; set; }
    public required string PlayerToken { get; set; }
    public required string Name { get; set; }
    public string? Backstory { get; set; }
    public int Strength { get; set; } = 1;
    public int Wit { get; set; } = 1;
    public int Charisma { get; set; } = 1;
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Player> Players { get; set; } = [];
    public ICollection<GameResult> GameResults { get; set; } = [];
}
