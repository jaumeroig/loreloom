using System.ComponentModel.DataAnnotations;
using LoreLoom.Core.Enums;

namespace LoreLoom.Core.Dtos;

public record CreateGameRequest(
    [Required, MaxLength(200)] string Title,
    [Required] string Setting,
    [Required, MaxLength(200)] string ResourceName,
    [Required] string CreatorToken,
    [Required, MaxLength(100)] string CreatorName,
    Guid? CharacterId,
    bool IsPublic = true,
    int MaxPlayers = 4,
    string Culture = Localization.AppCultures.DefaultCulture
);

public record JoinGameRequest(
    [Required] string Token,
    [Required, MaxLength(100)] string Name,
    Guid? CharacterId,
    string? InviteCode
);

public record StartGameRequest(
    [Required] string Token,
    string Culture = Localization.AppCultures.DefaultCulture
);

public record GameResponse(
    Guid Id,
    string Title,
    string Setting,
    string Culture,
    string ResourceName,
    int ResourcePct,
    bool IsPublic,
    string? InviteCode,
    int MaxPlayers,
    GameStatus Status,
    int PlayerCount,
    bool IsOwnedByCurrentUser,
    List<PlayerResponse> Players,
    DateTime CreatedAt
);

public record PlayerResponse(
    Guid Id,
    string Name,
    bool IsCurrentTurn,
    bool IsCreator,
    Guid? CharacterId
);

public record SendTurnRequest(
    [Required] string Token,
    [Required] string Action,
    string Culture = Localization.AppCultures.DefaultCulture
);

public record TurnResponse(
    Guid Id,
    string PlayerName,
    string PlayerAction,
    string DmResponse,
    int ResourceCost,
    DateTime CreatedAt
);

public record TurnResultResponse(
    string Narrative,
    int ResourceCost,
    int ResourcePctAfter,
    bool Victory,
    bool GameOver,
    string? Postmortem,
    List<XpAwardResponse>? XpPerPlayer
);

public record XpAwardResponse(string PlayerName, int Xp, string Reason);

public record GameResultResponse(
    Guid GameId,
    string GameTitle,
    bool Victory,
    string? Postmortem,
    List<PlayerResultResponse> Players
);

public record PlayerResultResponse(
    string Name,
    int XpEarned,
    int PointsEarned,
    string? CharacterName,
    int? CharacterLevel
);

public record RankingEntry(
    int Rank,
    string PlayerName,
    string? CharacterName,
    int TotalPoints,
    int GamesPlayed
);
