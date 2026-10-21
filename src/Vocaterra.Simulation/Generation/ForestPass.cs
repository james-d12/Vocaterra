using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 4: trees on the surface. The world is cut into cells of 8 columns, four per chunk column; each cell may hold one tree at a seeded
/// offset, so two trunks are always at least three columns apart. A tree is a trunk of 4 to 8 tiles on a grass tile of a flat-enough
/// column, topped with a leaf canopy that is written only into air. Every cell draws the same numbers whether or not it gets a tree, so
/// a changed rule in one cell never shifts the draws of the next.
/// </summary>
internal sealed class ForestPass : IGenerationPass
{
    private const int CellWidth = 8;
    private const int TreeChancePercent = 55;
    private const int MinTrunk = 4;
    private const int MaxTrunkExclusive = 9;

    public string Name => "forest";

    public void Run(GenerationContext context)
    {
        var tiles = context.Tiles;
        var surface = context.Record.SurfaceY;
        var width = context.Size.Width;

        for (var cx = 0; cx < tiles.ChunksX; cx++)
        {
            var rng = context.Rng(Name, cx);
            for (var cell = 0; cell < Chunk.Size / CellWidth; cell++)
            {
                var roll = rng.NextInt(100);
                var offset = rng.NextInt(CellWidth - 2);
                var trunkHeight = rng.NextInt(MinTrunk, MaxTrunkExclusive);
                if (roll >= TreeChancePercent)
                {
                    continue;
                }

                var x = (cx * Chunk.Size) + (cell * CellWidth) + 1 + offset;
                if (x < 3 || x > width - 4)
                {
                    continue;
                }

                var ground = surface[x];
                if (tiles.GetTile(x, ground) != TileId.Grass
                    || Math.Abs(surface[x - 1] - ground) > 1
                    || Math.Abs(surface[x + 1] - ground) > 1
                    || !TrunkSpaceIsFree(tiles, x, ground, trunkHeight))
                {
                    continue;
                }

                PlantTree(tiles, x, ground, trunkHeight);
                context.Record.AddTree(new TilePoint(x, ground));
            }
        }
    }

    internal static bool TrunkSpaceIsFree(TileGrid tiles, int x, int ground, int trunkHeight)
    {
        if (ground - trunkHeight - 2 < 0)
        {
            return false;
        }

        for (var y = ground - trunkHeight; y < ground; y++)
        {
            if (tiles.GetTile(x, y) != TileId.Air)
            {
                return false;
            }
        }

        return true;
    }

    internal static void PlantTree(TileGrid tiles, int x, int ground, int trunkHeight)
    {
        var top = ground - trunkHeight;

        // Canopy rows relative to the top trunk tile: two wide-short rows above, the middle rows 5 wide, one narrow row below.
        for (var dy = -2; dy <= 1; dy++)
        {
            var reach = dy is -2 or 1 ? 1 : 2;
            for (var dx = -reach; dx <= reach; dx++)
            {
                if (tiles.GetTile(x + dx, top + dy) == TileId.Air)
                {
                    tiles.SetTile(x + dx, top + dy, TileId.Leaves);
                }
            }
        }

        for (var y = top; y < ground; y++)
        {
            tiles.SetTile(x, y, TileId.Trunk);
        }
    }
}
