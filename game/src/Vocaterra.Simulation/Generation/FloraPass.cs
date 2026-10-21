using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 8: flowers and mushrooms standing on the grass. Every column draws one number from its chunk's stream (whether or not it gets a
/// plant, so a change in one column never shifts the draws of the next): below 7 a flower, below 10 a mushroom, on the air tile above a grass
/// surface. Columns within two of a tree base and within three of the spawn column are left alone, as are columns whose surface is sand.
/// Runs after the sand pass.
/// </summary>
internal sealed class FloraPass : IGenerationPass
{
    private const int FlowerBelow = 7;
    private const int MushroomBelow = 10;
    private const int TallGrassBelow = 40;
    private const int TreeClearance = 2;
    private const int SpawnClearance = 3;

    public string Name => "flora";

    public void Run(GenerationContext context)
    {
        var tiles = context.Tiles;
        var surface = context.Record.SurfaceY;
        var width = context.Size.Width;
        var spawn = context.Spawn ?? throw new InvalidOperationException("The spawn pass must run before the flora pass.");
        var nearTree = TreeColumns.Mark(context.Record, width, TreeClearance);

        for (var cx = 0; cx < tiles.ChunksX; cx++)
        {
            var rng = context.Rng(Name, cx);
            for (var column = 0; column < Chunk.Size; column++)
            {
                var roll = rng.NextInt(100);
                var x = (cx * Chunk.Size) + column;
                if (x < 2 || x > width - 3 || nearTree[x] || Math.Abs(x - spawn.X) <= SpawnClearance)
                {
                    continue;
                }

                var ground = surface[x];
                if (tiles.GetTile(x, ground) != TileId.Grass || tiles.GetTile(x, ground - 1) != TileId.Air)
                {
                    continue;
                }

                if (roll < FlowerBelow)
                {
                    tiles.SetTile(x, ground - 1, TileId.Flower);
                }
                else if (roll < MushroomBelow)
                {
                    tiles.SetTile(x, ground - 1, TileId.Mushroom);
                }
                else if (roll < TallGrassBelow)
                {
                    tiles.SetTile(x, ground - 1, TileId.TallGrass);
                }
            }
        }
    }
}
