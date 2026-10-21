using Vocaterra.Game;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game.Tests;

/// <summary>Shared set-up: the game bundle loaded once, and sessions on a fixed seed and a fixed stored time.</summary>
internal static class TestGame
{
    /// <summary>The seed every guide test uses unless it says otherwise.</summary>
    public const ulong Seed = 42;

    private static readonly Lazy<ContentBundle> LoadedBundle = new(() => GameBundle.Load());

    /// <summary>The bundle, loaded and validated by the core's loader.</summary>
    public static ContentBundle Bundle => LoadedBundle.Value;

    /// <summary>A new session on the fixed seed, with the fixed start time.</summary>
    public static GameSession NewSession(ulong seed = Seed) => new(Bundle, seed, GameSession.DefaultStartedAt);

    /// <summary>
    /// The nearest tile of a kind by brute force over the whole grid, with the guide's rule written out again: the smallest squared distance,
    /// then the smaller row, then the smaller column.
    /// </summary>
    public static TilePoint BruteForceNearest(World world, TilePoint from, params TileId[] kinds)
    {
        TilePoint? best = null;
        long bestDistance = long.MaxValue;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (!kinds.Contains(world.GetTile(x, y)))
                {
                    continue;
                }

                var distance = (long)((x - from.X) * (x - from.X)) + ((y - from.Y) * (y - from.Y));
                if (distance < bestDistance || (distance == bestDistance && (y, x).CompareTo((best!.Value.Y, best.Value.X)) < 0))
                {
                    bestDistance = distance;
                    best = new TilePoint(x, y);
                }
            }
        }

        return best ?? throw new InvalidOperationException("No such tile in the world.");
    }
}
