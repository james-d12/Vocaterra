using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Noise;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The cave linings (B11b, <see cref="LiningSpec"/>, D-game-34 item 27): a rock cell that a cave exposes (air above it: a floor; air at its side: a wall; air under it: a ceiling) may become another block by the
/// first rule of the data that applies: gravel and loose stone on the floors, mossy stone near an entrance's passage (the damp zone: <see cref="LiningSpec.DampReach"/> blocks round it) and near water (the
/// wet zone: a water column within <see cref="LiningSpec.WetReach"/> columns), sand and gravel where the cave lies near water. A rule has a band of heights, the rocks it replaces, a share and
/// an optional patchy mask, so the blocks lie in clumps. Exposure is read inside the chunk only (a cell on the chunk's edge is judged by its inner faces), and air above the ground is not a cave: so a chunk is the
/// same whichever chunks were made before. It runs after the ores, so a vein is never covered.
/// </summary>
internal sealed class LiningPass : IChunkDecorator
{
    private const ulong GoldenX = 0x9E3779B97F4A7C15UL;
    private const ulong GoldenY = 0xC2B2AE3D27D4EB4FUL;
    private const ulong GoldenZ = 0x165667B19E3779F9UL;

    private readonly Rule[] _rules;
    private readonly BlockId _lava;
    private readonly bool[] _eligible;
    private readonly int _lowest;
    private readonly int _highest;
    private readonly int _dampReach;
    private readonly ulong _key;

    public LiningPass(GenerationWorld world)
    {
        var spec = world.Underground!.Data.Linings!;
        _lava = world.Underground.Lava;
        _dampReach = spec.DampReach;
        _rules = [.. spec.Rules.Select((r, i) => new Rule(r, world.Seed, i))];
        _eligible = new bool[BlockKinds.Capacity];
        foreach (var rule in _rules)
        {
            foreach (var rock in rule.In)
            {
                _eligible[(int)rock] = true;
            }
        }

        _lowest = _rules.Length == 0 ? int.MaxValue : _rules.Min(static r => r.MinY);
        _highest = _rules.Length == 0 ? int.MinValue : _rules.Max(static r => r.MaxY);
        _key = WorldRng.For(world.Seed, Name).NextUInt64();
    }

    /// <summary>The pass's name, which is also the key of its hash.</summary>
    public const string Name = "gen/linings";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        if (chunk.MaxY < _lowest || chunk.MinY > _highest)
        {
            return;
        }

        var any = false;
        foreach (var block in chunk.Blocks)
        {
            if (block == BlockId.Air)
            {
                any = true;
                break;
            }
        }

        if (!any)
        {
            return;
        }

        var plan = ctx.CavePlan;
        var (damp, dampLow, dampHigh) = DampColumns(ctx, chunk);
        var heights = new int[VoxelChunk.Size * VoxelChunk.Size];
        for (var lz = 0; lz < VoxelChunk.Size; lz++)
        {
            for (var lx = 0; lx < VoxelChunk.Size; lx++)
            {
                heights[(lz * VoxelChunk.Size) + lx] = ctx.HeightAt(chunk.MinX + lx, chunk.MinZ + lz);
            }
        }

        // Collected first and written after, so a cell's neighbours are read as the caves left them.
        var writes = new List<(int Index, BlockId Block)>();
        for (var ly = 0; ly < VoxelChunk.Size; ly++)
        {
            var y = chunk.MinY + ly;
            if (y < _lowest || y > _highest)
            {
                continue;
            }

            for (var lz = 0; lz < VoxelChunk.Size; lz++)
            {
                for (var lx = 0; lx < VoxelChunk.Size; lx++)
                {
                    var here = chunk.GetLocal(lx, ly, lz);
                    if (!_eligible[(int)here])
                    {
                        continue;
                    }

                    var exposure = Exposure(chunk, heights, lx, ly, lz, _lava);
                    if (exposure == Faces.None)
                    {
                        continue;
                    }

                    var k = (lz * VoxelChunk.Size) + lx;
                    var inDamp = damp && y >= dampLow[k] && y <= dampHigh[k];
                    var pick = Pick(chunk.MinX + lx, y, chunk.MinZ + lz, here, exposure, inDamp, plan.Wet[k]);
                    if (pick != BlockId.Air)
                    {
                        writes.Add((((ly * VoxelChunk.Size) + lz) * VoxelChunk.Size + lx, pick));
                    }
                }
            }
        }

        foreach (var (index, block) in writes)
        {
            chunk.Set(chunk.MinX + (index & (VoxelChunk.Size - 1)), chunk.MinY + (index >> (2 * VoxelChunk.Shift)), chunk.MinZ + ((index >> VoxelChunk.Shift) & (VoxelChunk.Size - 1)), block);
        }
    }

    [Flags]
    private enum Faces
    {
        None = 0,
        Floor = 1,
        Wall = 2,
        Ceiling = 4,
    }

    // Which faces of a rock cell a cave exposes, from the cells of the chunk (outside it nothing is known, so nothing is exposed): cave air is air or lava at or under the ground of its own column.
    private static Faces Exposure(ChunkBuilder chunk, int[] heights, int lx, int ly, int lz, BlockId lava)
    {
        var faces = Faces.None;
        if (OpenAt(chunk, heights, lx, ly + 1, lz, lava))
        {
            faces |= Faces.Floor;
        }

        if (OpenAt(chunk, heights, lx, ly - 1, lz, lava))
        {
            faces |= Faces.Ceiling;
        }

        if (OpenAt(chunk, heights, lx + 1, ly, lz, lava) || OpenAt(chunk, heights, lx - 1, ly, lz, lava) || OpenAt(chunk, heights, lx, ly, lz + 1, lava) || OpenAt(chunk, heights, lx, ly, lz - 1, lava))
        {
            faces |= Faces.Wall;
        }

        return faces;
    }

    private static bool OpenAt(ChunkBuilder chunk, int[] heights, int lx, int ly, int lz, BlockId lava)
    {
        if ((uint)lx >= VoxelChunk.Size || (uint)ly >= VoxelChunk.Size || (uint)lz >= VoxelChunk.Size)
        {
            return false;
        }

        var block = chunk.GetLocal(lx, ly, lz);
        return (block == BlockId.Air || block == lava) && chunk.MinY + ly <= heights[(lz * VoxelChunk.Size) + lx];
    }

    private BlockId Pick(int x, int y, int z, BlockId here, Faces exposure, bool damp, bool wet)
    {
        for (var i = 0; i < _rules.Length; i++)
        {
            var rule = _rules[i];
            if (y < rule.MinY || y > rule.MaxY || Array.IndexOf(rule.In, here) < 0 || !rule.Matches(exposure) || (rule.Zone == Zone.Damp && !damp) || (rule.Zone == Zone.Wet && !wet))
            {
                continue;
            }

            var hash = WorldRng.Mix(_key ^ ((ulong)(uint)x * GoldenX) ^ ((ulong)(uint)y * GoldenY) ^ ((ulong)(uint)z * GoldenZ) ^ ((ulong)(i + 1) * GoldenX));
            if ((int)(hash % 1000UL) < rule.Chance && (rule.Patch is null || rule.Patch.At(x, y, z) > rule.Threshold))
            {
                return rule.Block;
            }
        }

        return BlockId.Air;
    }

    // For each column of the chunk, the heights between which it lies within the damp reach of an entrance's passage (none when no passage is near).
    private (bool Any, int[] Low, int[] High) DampColumns(ColumnContext ctx, ChunkBuilder chunk)
    {
        var low = new int[VoxelChunk.Size * VoxelChunk.Size];
        var high = new int[VoxelChunk.Size * VoxelChunk.Size];
        Array.Fill(low, int.MaxValue);
        Array.Fill(high, int.MinValue);
        var any = false;
        if (_dampReach <= 0)
        {
            return (false, low, high);
        }

        foreach (var ramp in ctx.Ramps)
        {
            for (var i = 0; i <= ramp.Last; i++)
            {
                var (cx, cz) = ramp.CentreAt(i);
                var floor = ramp.FloorAt(i);
                var (y0, y1) = (floor - _dampReach, floor + ramp.Headroom + _dampReach);
                if (y1 < chunk.MinY || y0 > chunk.MaxY)
                {
                    continue;
                }

                for (var z = Math.Max(cz - _dampReach, chunk.MinZ); z <= Math.Min(cz + _dampReach, chunk.MinZ + VoxelChunk.Size - 1); z++)
                {
                    for (var x = Math.Max(cx - _dampReach, chunk.MinX); x <= Math.Min(cx + _dampReach, chunk.MinX + VoxelChunk.Size - 1); x++)
                    {
                        var k = ((z - chunk.MinZ) * VoxelChunk.Size) + (x - chunk.MinX);
                        low[k] = Math.Min(low[k], y0);
                        high[k] = Math.Max(high[k], y1);
                        any = true;
                    }
                }
            }
        }

        return (any, low, high);
    }

    private enum Zone
    {
        Any,
        Damp,
        Wet,
    }

    // One rule, resolved: its blocks, faces, zone, band and the patch's noise.
    private sealed class Rule
    {
        private readonly Faces _faces;

        public Rule(LiningRule rule, ulong seed, int index)
        {
            Block = UndergroundWorld.BlockNamed(rule.Block);
            In = [.. rule.In.Select(UndergroundWorld.BlockNamed)];
            _faces = rule.Face switch
            {
                "floor" => Faces.Floor,
                "wall" => Faces.Wall,
                "ceiling" => Faces.Ceiling,
                _ => Faces.Floor | Faces.Wall | Faces.Ceiling,
            };
            Zone = rule.Zone switch
            {
                "damp" => Zone.Damp,
                "wet" => Zone.Wet,
                _ => Zone.Any,
            };
            Chance = rule.Chance;
            MinY = rule.MinY;
            MaxY = rule.MaxY;
            if (rule.Patch is { } patch)
            {
                Patch = new NoiseField3(seed, $"gen/linings/{index}/{rule.Block}", patch.Cell, patch.CellY, patch.Octaves);
                Threshold = FieldScale.FromThousandths(patch.Threshold);
            }
        }

        public BlockId Block { get; }

        public BlockId[] In { get; }

        public Zone Zone { get; }

        public int Chance { get; }

        public int MinY { get; }

        public int MaxY { get; }

        public NoiseField3? Patch { get; }

        public int Threshold { get; }

        public bool Matches(Faces exposure) => (_faces & exposure) != Faces.None;
    }
}
