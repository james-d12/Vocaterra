using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 5: the spawn tile. Starting at the middle column and moving outwards (right, left, right, ...), the first column whose neighbours
/// on both sides are at the same height, whose ground is grass and which has four tiles of air above the three columns wins. The spawn
/// tile is the air tile directly above that grass.
/// </summary>
internal sealed class SpawnPass : IGenerationPass
{
    private const int Headroom = 4;

    public string Name => "spawn";

    public void Run(GenerationContext context)
    {
        var width = context.Size.Width;
        var centre = width / 2;
        for (var step = 0; step < width; step++)
        {
            var offset = (step + 1) / 2;
            var x = centre + ((step & 1) == 1 ? offset : -offset);
            if (x < 2 || x > width - 3)
            {
                continue;
            }

            if (IsGoodSpawnColumn(context, x))
            {
                context.Spawn = new TilePoint(x, context.Record.SurfaceY[x] - 1);
                return;
            }
        }

        throw new InvalidOperationException("No column of this world can hold the player spawn.");
    }

    private static bool IsGoodSpawnColumn(GenerationContext context, int x)
    {
        var surface = context.Record.SurfaceY;
        var ground = surface[x];
        if (surface[x - 1] != ground || surface[x + 1] != ground || context.Tiles.GetTile(x, ground) != TileId.Grass)
        {
            return false;
        }

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = 1; dy <= Headroom; dy++)
            {
                if (context.Tiles.GetTile(x + dx, ground - dy) != TileId.Air)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
