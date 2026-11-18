using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The default decoration strategy (B03): the row's <c>decor</c> blocks, sown by noise. A column has a plant when a hash of it falls under the row's density (in thousandths), thinned and
/// thickened by a slow noise so the plants come in patches; the plant is chosen by weight from a second hash. Only grass and sand carry plants.
/// </summary>
internal sealed class UndergrowthStrategy : IDecorationStrategy
{
    private const int PatchCell = 18;
    private readonly TerrainNoise _noise;
    private readonly Sown[] _rows;

    public UndergrowthStrategy(in StrategyContext context)
    {
        _noise = new TerrainNoise(context.Seed, UndergrowthPass.Name);
        _rows = [.. context.Biomes.Rows.Select(static r => new Sown(r))];
    }

    /// <inheritdoc />
    public string Name => StrategyRegistry.UndergrowthDecoration;

    /// <inheritdoc />
    public BlockId PlantAt(in DecorationColumn column)
    {
        var sown = _rows[column.Row.Index];
        if (sown.Total == 0 || (column.Ground != BlockId.Grass && column.Ground != BlockId.Sand))
        {
            return BlockId.Air;
        }

        var patch = 70 + (_noise.Value(3, column.X, column.Z, PatchCell) * 30 / 4096); // 70 to 100 percent of the density
        var pick = _noise.Column(10, column.X, column.Z) * 1000 / 4096; // 0 to 999
        if (pick * 100 >= column.Row.Decor.Density * patch)
        {
            return BlockId.Air;
        }

        var kind = _noise.Column(11, column.X, column.Z) * sown.Total / 4096;
        foreach (var (block, upTo) in sown.Cumulative)
        {
            if (kind < upTo)
            {
                return block;
            }
        }

        return BlockId.Air;
    }

    // A row's plants resolved once: the block of each name and the running total of the weights.
    private sealed class Sown
    {
        public Sown(BiomeRow row)
        {
            var total = 0;
            var list = new List<(BlockId, int)>();
            foreach (var entry in row.Decor.Blocks)
            {
                total += entry.Weight;
                list.Add((SceneLayoutParser.BlockNamed(entry.Block, $"biome '{row.Id}' undergrowth"), total));
            }

            Cumulative = [.. list];
            Total = row.Decor.Density > 0 ? total : 0;
        }

        public (BlockId Block, int UpTo)[] Cumulative { get; }

        public int Total { get; }
    }
}
