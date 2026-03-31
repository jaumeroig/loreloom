using System.ComponentModel.DataAnnotations;

namespace LoreLoom.Core.Dtos;

public record CreateCharacterRequest(
    [Required] string PlayerToken,
    [Required, MaxLength(100)] string Name,
    string? Backstory,
    [Range(1, 5)] int Strength = 3,
    [Range(1, 5)] int Wit = 3,
    [Range(1, 5)] int Charisma = 3
);

public record UpdateCharacterRequest(
    [Range(1, 5)] int? Strength,
    [Range(1, 5)] int? Wit,
    [Range(1, 5)] int? Charisma
);

public record CharacterResponse(
    Guid Id,
    string Name,
    string? Backstory,
    int Strength,
    int Wit,
    int Charisma,
    int Level,
    int Xp,
    int XpToNextLevel,
    DateTime CreatedAt,
    DateTime LastPlayedAt
);
