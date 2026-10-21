using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 10: ore veins in the stone. Each ore has a depth band below the surface column and a chance per chunk; a vein is a seeded random walk
/// of 4 to 9 cells that turns stone into ore, and only stone inside the band. Coal is shallow and common, gold is deep and rare:
/// coal 8 to 60 tiles down (35 % of chunks), copper 20 to 90 (30 %), iron 40 to 150 (25 %), gold 110 to 260 (15 %). Dirt is never ore.
/// Runs after the caves, so an ore can sit in a cave wall, and each ore draws only from its own per-chunk streams.
/// </summary>
internal sealed class OrePass : IGenerationPass
{
    private const int MinCells = 4;
    private const int MaxCellsExclusive = 10;

    private static readonly Vein[] Veins =
    [
        new("coal", TileId.CoalOre, 8, 60, 35),
        new("copper", TileId.CopperOre, 20, 90, 30),
        new("iron", TileId.IronOre, 40, 150, 25),
        new("gold", TileId.GoldOre, 110, 260, 15),
    ];

    /// <inheritdoc />
    public string Name => "ores";

    /// <inheritdoc />
    public void Run(GenerationContext context)
    {
        var tiles = context.Tiles;
        var surface = context.Record.SurfaceY;
        foreach (var vein in Veins)
        {
            for (var cy = 0; cy < tiles.ChunksY; cy++)
            {
                for (var cx = 0; cx < tiles.ChunksX; cx++)
                {
                    var rng = context.Rng(Name + "." + vein.Name, cx, cy);
                    var roll = rng.NextInt(100);
                    var x = (cx * Chunk.Size) + rng.NextInt(Chunk.Size);
                    var y = (cy * Chunk.Size) + rng.NextInt(Chunk.Size);
                    var cells = rng.NextInt(MinCells, MaxCellsExclusive);
                    if (roll >= vein.ChancePercent)
                    {
                        continue;
                    }

                    for (var i = 0; i < cells; i++)
                    {
                        if (x >= 1 && x < tiles.Width - 1 && y >= 1 && y < tiles.Height - 1)
                        {
                            var depth = y - surface[x];
                            if (depth >= vein.MinDepth && depth <= vein.MaxDepth && tiles.GetTile(x, y) == TileId.Stone)
                            {
                                tiles.SetTile(x, y, vein.Tile);
                            }
                        }

                        var (dx, dy) = Step(rng.NextInt(4));
                        x += dx;
                        y += dy;
                    }
                }
            }
        }
    }

    private static (int Dx, int Dy) Step(int direction) => direction switch
    {
        0 => (1, 0),
        1 => (-1, 0),
        2 => (0, 1),
        _ => (0, -1),
    };

    /// <summary>One ore: its stream name, its tile, the depth band (tiles below the surface column) and the chance per chunk in percent.</summary>
    private readonly record struct Vein(string Name, TileId Tile, int MinDepth, int MaxDepth, int ChancePercent);
}
