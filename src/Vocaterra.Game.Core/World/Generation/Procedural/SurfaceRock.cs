using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Noise;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The bare stone and the loose stone of the surface (B11b, <see cref="SurfaceRockSpec"/>, D-game-34 item 27): a column that stands <see cref="CliffSpec.Drop"/> blocks over its lowest neighbour (water counts
/// as its surface) is a cliff column, whose exposed layers show banded rock (<see cref="BandAt"/>) instead of the grass and dirt of the biome; steeper than the scree's drop, or at a cliff's foot, the top
/// is loose stone (gravel); the beds and banks a river carved get gravel and sand in patches. It never touches a column of snow, and a column that is none of these keeps the biome's own layers. A pure
/// function of the seed, the data and the column's neighbourhood in the window (one block for the drop, the foot's reach for the scree).
/// </summary>
internal sealed class SurfaceRock
{
    private const ulong GoldenX = 0x9E3779B97F4A7C15UL;
    private const ulong GoldenY = 0xC2B2AE3D27D4EB4FUL;
    private const ulong GoldenZ = 0x165667B19E3779F9UL;

    private readonly SurfaceRockSpec _spec;
    private readonly BlockId[] _bands;
    private readonly int[] _bandWeights;
    private readonly int _bandTotal;
    private readonly Scatter[] _scree;
    private readonly Scatter[] _streambed;
    private readonly NoiseStack _tilt;
    private readonly ulong _key;
    private readonly int _minWeight;

    /// <summary>Creates the surface rock of one world seed.</summary>
    /// <param name="spec">The data.</param>
    /// <param name="seed">The world seed.</param>
    /// <exception cref="ArgumentException">A block name is not a block of the game.</exception>
    public SurfaceRock(SurfaceRockSpec spec, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _spec = spec;
        _bands = [.. spec.Cliff.Bands.Select(static b => UndergroundWorld.BlockNamed(b.Block))];
        _bandWeights = [.. spec.Cliff.Bands.Select(static b => b.Weight)];
        _bandTotal = _bandWeights.Sum();
        _tilt = new NoiseStack(seed, "gen/surface-rock/tilt");
        _key = WorldRng.For(seed, "gen/surface-rock").NextUInt64();
        _scree = [.. spec.Scree.Rules.Select((r, i) => new Scatter(r, seed, $"gen/surface-rock/scree/{i}"))];
        _streambed = [.. spec.Streambed.Rules.Select((r, i) => new Scatter(r, seed, $"gen/surface-rock/streambed/{i}"))];
        _minWeight = (int)((long)spec.Streambed.MinWeight * IntMath.One / 1000);
    }

    /// <summary>How many columns round a column the scree reads.</summary>
    public int Reach => Math.Max(1, _spec.Scree.FootReach);

    /// <summary>How far a dry column stands over its lowest neighbour (four sides): zero when it does not, or when it holds water.</summary>
    /// <param name="ctx">The shaped column.</param>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int Drop(ColumnContext ctx, int x, int z) => ctx.WaterLevelAt(x, z) >= 0 ? 0 : Math.Max(0, ctx.HeightAt(x, z) - LowestNeighbour(ctx, x, z));

    /// <summary>The visible surface of the lowest of a column's four neighbours (the ground, or the water's surface above it); the column's own height when a neighbour is outside the window.</summary>
    /// <param name="ctx">The shaped column.</param>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int LowestNeighbour(ColumnContext ctx, int x, int z)
    {
        var low = ctx.HeightAt(x, z);
        Span<(int, int)> sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        foreach (var (dx, dz) in sides)
        {
            if (ctx.InWindow(x + dx, z + dz))
            {
                low = Math.Min(low, Math.Max(ctx.HeightAt(x + dx, z + dz), ctx.WaterLevelAt(x + dx, z + dz)));
            }
        }

        return low;
    }

    /// <summary>Whether a column is a cliff column.</summary>
    /// <param name="ctx">The shaped column.</param>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public bool IsCliff(ColumnContext ctx, int x, int z) => Drop(ctx, x, z) >= _spec.Cliff.Drop;

    /// <summary>
    /// The layers of a column after the surface rock: the column unchanged when no rule applies, else with the top (and for scree the filler) one of the rock's blocks and the column marked bare.
    /// </summary>
    /// <param name="ctx">The shaped column.</param>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="surface">The column as the biome made it.</param>
    public ColumnSurface Adjust(ColumnContext ctx, int x, int z, in ColumnSurface surface)
    {
        var shape = ctx.ShapeAt(x, z);
        if (surface.Water)
        {
            return shape.RiverWeight >= _minWeight && Pick(_streambed, x, z) is { } bed ? surface with { Top = bed } : surface;
        }

        if (surface.Top == BlockId.Snow)
        {
            return surface;
        }

        var drop = Drop(ctx, x, z);
        if (drop >= _spec.Cliff.Drop)
        {
            return surface with { Top = BandAt(x, surface.Height, z), Bare = true };
        }

        if (drop >= _spec.Scree.SteepDrop || AtFoot(ctx, x, z))
        {
            if (Pick(_scree, x, z) is { } loose)
            {
                return surface with { Top = loose, Filler = loose, Bare = true };
            }
        }

        if (shape.RiverWeight >= _minWeight && Pick(_streambed, x, z) is { } bank)
        {
            return surface with { Top = bank, Bare = true };
        }

        return surface;
    }

    /// <summary>The rock of a cliff's band at a block: bands a few blocks thick, tilted across the face, their rocks drawn by weight per zone.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    public BlockId BandAt(int x, int y, int z)
    {
        var c = _spec.Cliff;
        var tilt = (int)((long)_tilt.Fbm(x, z, Math.Max(8, c.ZoneCell / 2), 1) * c.Tilt / IntMath.One);
        var index = IntMath.DivDown(y + tilt, c.BandThickness);
        var hash = WorldRng.Mix(_key ^ ((ulong)(uint)index * GoldenY) ^ ((ulong)(uint)IntMath.DivDown(x, c.ZoneCell) * GoldenX) ^ ((ulong)(uint)IntMath.DivDown(z, c.ZoneCell) * GoldenZ));
        var pick = (int)(hash % (ulong)_bandTotal);
        for (var i = 0; i < _bands.Length; i++)
        {
            if (pick < _bandWeights[i])
            {
                return _bands[i];
            }

            pick -= _bandWeights[i];
        }

        return _bands[^1];
    }

    // A column at a cliff's foot: ground that a wall of at least the cliff's drop stands over within the foot's reach.
    private bool AtFoot(ColumnContext ctx, int x, int z)
    {
        var reach = _spec.Scree.FootReach;
        var height = ctx.HeightAt(x, z);
        for (var dz = -reach; dz <= reach; dz++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                if ((dx != 0 || dz != 0) && ctx.InWindow(x + dx, z + dz) && ctx.HeightAt(x + dx, z + dz) - height >= _spec.Cliff.Drop)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private BlockId? Pick(Scatter[] rules, int x, int z)
    {
        for (var i = 0; i < rules.Length; i++)
        {
            if (rules[i].Applies(_key, i, x, z))
            {
                return rules[i].Block;
            }
        }

        return null;
    }

    // One scattered block: a hash of the column against its chance, inside the rule's patch.
    private sealed class Scatter
    {
        private readonly int _chance;
        private readonly NoiseStack? _patch;
        private readonly int _cell;
        private readonly int _octaves;
        private readonly int _threshold;

        public Scatter(ScatterRule rule, ulong seed, string name)
        {
            Block = UndergroundWorld.BlockNamed(rule.Block);
            _chance = rule.Chance;
            if (rule.Patch is { } patch)
            {
                _patch = new NoiseStack(seed, name);
                _cell = patch.Cell;
                _octaves = patch.Octaves;
                _threshold = FieldScale.FromThousandths(patch.Threshold);
            }
        }

        public BlockId Block { get; }

        public bool Applies(ulong key, int rule, int x, int z)
        {
            var hash = WorldRng.Mix(key ^ ((ulong)(uint)x * GoldenX) ^ ((ulong)(uint)z * GoldenZ) ^ ((ulong)(rule + 1) * GoldenY));
            return (int)(hash % 1000UL) < _chance && (_patch is null || _patch.Fbm(x, z, _cell, _octaves) > _threshold);
        }
    }
}
