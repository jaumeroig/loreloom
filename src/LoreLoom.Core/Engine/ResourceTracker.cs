using LoreLoom.Core.Enums;
using LoreLoom.Core.Models;

namespace LoreLoom.Core.Engine;

public static class ResourceTracker
{
    public const int DefaultCost = 7;

    public static int ApplyCost(Game game, int cost)
    {
        game.ResourcePct = Math.Max(0, game.ResourcePct - cost);

        if (game.ResourcePct <= 0)
            game.Status = GameStatus.Finished;

        return game.ResourcePct;
    }

    public static bool IsGameOver(Game game) => game.ResourcePct <= 0;
}
