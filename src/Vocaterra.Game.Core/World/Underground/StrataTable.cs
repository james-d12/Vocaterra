using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>What rock lies at a place (B11, biomes plan section 3.5): stone, slate, granite or limestone, a pure function of the position.</summary>
public interface IStrataSelector
{
    /// <summary>The rock at a block of the stone layers (the ground's own top and filler layers are the biome's).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="y">The block's y.</param>
    /// <param name="z">The block's z.</param>
    BlockId RockAt(int x, int y, int z);
}

/// <summary>
/// The rock layers (<see cref="StrataSpec"/>): stone down to <see cref="StrataSpec.DitherToY"/>, the deep rock below <see cref="StrataSpec.DitherFromY"/>, a change between them dithered cell by
/// cell by a hash of the position, and blobs of other rock in the stone layer where a 3D noise field passes its threshold (the first blob that does wins). <see cref="Fill"/> gives a whole chunk's
/// rocks from the blob noise sampled once on a coarse lattice, and the same numbers <see cref="RockAt"/> gives one by one. Pure, immutable and safe from any thread.
/// </summary>
internal sealed class StrataTable : IStrataSelector
{
    private const ulong GoldenX = 0x9E3779B97F4A7C15UL;
    private const ulong GoldenY = 0xC2B2AE3D27D4EB4FUL;
    private const ulong GoldenZ = 0x165667B19E3779F9UL;

    private readonly StrataSpec _spec;
    private readonly BlockId _stone;
    private readonly BlockId _deep;
    private readonly BlockId[] _blobBlocks;
    private readonly int[] _blobThresholds;
    private readonly NoiseField3[] _blobFields;
    private readonly ulong _key;

    /// <summary>Creates the table of one world seed.</summary>
    /// <param name="spec">The layers.</param>
    /// <param name="seed">The world seed.</param>
    /// <exception cref="ArgumentException">A block name is not a block of the game.</exception>
    public StrataTable(StrataSpec spec, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _spec = spec;
        _stone = UndergroundWorld.BlockNamed(spec.Stone);
        _deep = UndergroundWorld.BlockNamed(spec.Deep);
        _key = WorldRng.For(seed, "gen/strata").NextUInt64();
        _blobBlocks = [.. spec.Blobs.Select(static b => UndergroundWorld.BlockNamed(b.Block))];
        _blobThresholds = [.. spec.Blobs.Select(static b => FieldScale.FromThousandths(b.Threshold))];
        _blobFields = [.. spec.Blobs.Select((b, i) => new NoiseField3(seed, $"gen/strata/blob/{i}/{b.Block}", b.Cell, b.CellY, b.Octaves))];
    }

    /// <summary>Whether any blob exists, so a chunk's rocks are not all one block above the dither.</summary>
    public bool HasBlobs => _blobFields.Length > 0;

    /// <summary>The stone block.</summary>
    public BlockId Stone => _stone;

    /// <summary>The deep rock block.</summary>
    public BlockId Deep => _deep;

    /// <inheritdoc />
    public BlockId RockAt(int x, int y, int z)
    {
        var baseRock = BaseRock(x, y, z);
        if (baseRock != _stone)
        {
            return baseRock;
        }

        for (var i = 0; i < _blobFields.Length; i++)
        {
            if (LatticeGrid.At(_blobFields[i].At, _spec.BlobLattice, _spec.BlobLattice, x, y, z) > _blobThresholds[i])
            {
                return _blobBlocks[i];
            }
        }

        return _stone;
    }

    /// <summary>
    /// The rocks of the 16 x 16 x 16 box whose first block is (x0, y0, z0) (a chunk: its corner is a multiple of 16), in <c>ChunkData</c> order, the blob noise evaluated once on the lattice and
    /// interpolated only in the lattice cells where a blob can occur. The rocks equal <see cref="RockAt"/>'s one by one.
    /// </summary>
    /// <param name="x0">The box's first x.</param>
    /// <param name="y0">The box's first y.</param>
    /// <param name="z0">The box's first z.</param>
    /// <param name="into">The rocks, at least 4 096 of them.</param>
    public void Fill(int x0, int y0, int z0, Span<BlockId> into)
    {
        const int Size = VoxelChunk.Size;
        ArgumentOutOfRangeException.ThrowIfLessThan(into.Length, VoxelChunk.Volume);
        if (y0 >= _spec.DitherToY)
        {
            into[..VoxelChunk.Volume].Fill(_stone);
        }
        else if (y0 + Size <= _spec.DitherFromY)
        {
            into[..VoxelChunk.Volume].Fill(_deep);
        }
        else
        {
            var index = 0;
            for (var ly = 0; ly < Size; ly++)
            {
                for (var lz = 0; lz < Size; lz++)
                {
                    for (var lx = 0; lx < Size; lx++, index++)
                    {
                        into[index] = BaseRock(x0 + lx, y0 + ly, z0 + lz);
                    }
                }
            }
        }

        if (_blobFields.Length == 0 || y0 + Size - 1 < _spec.DitherToY)
        {
            return;
        }

        // The first blob row that passes wins, so the rows are applied from the last to the first; a blob replaces stone only.
        Span<sbyte> pick = stackalloc sbyte[VoxelChunk.Volume];
        pick.Fill(-1);
        var any = false;
        var cell = _spec.BlobLattice;
        for (var row = _blobFields.Length - 1; row >= 0; row--)
        {
            var grid = new LatticeGrid(_blobFields[row].At, cell, cell, x0, y0, z0, Size);
            for (var k = 0; k < grid.CellsXz; k++)
            {
                for (var j = 0; j < grid.CellsY; j++)
                {
                    for (var i = 0; i < grid.CellsXz; i++)
                    {
                        if (grid.Max(i, j, k) > _blobThresholds[row])
                        {
                            any |= MarkCell(grid, (i, j, k), row, pick);
                        }
                    }
                }
            }
        }

        if (!any)
        {
            return;
        }

        for (var at = 0; at < VoxelChunk.Volume; at++)
        {
            if (pick[at] >= 0 && into[at] == _stone)
            {
                into[at] = _blobBlocks[pick[at]];
            }
        }
    }

    // Marks the cells of a lattice cell where a blob row's noise passes its threshold; whether it marked any.
    private bool MarkCell(LatticeGrid grid, (int I, int J, int K) cell, int row, Span<sbyte> pick)
    {
        var size = _spec.BlobLattice;
        var marked = false;
        var whole = grid.Min(cell.I, cell.J, cell.K) > _blobThresholds[row]; // every corner passes, so every block in the cell does
        for (var dy = 0; dy < size; dy++)
        {
            var fy = dy * IntMath.One / size;
            for (var dz = 0; dz < size; dz++)
            {
                var fz = dz * IntMath.One / size;
                grid.Edges(cell.I, cell.J, cell.K, fy, fz, out var left, out var right);
                var at = ((((cell.J * size) + dy) * VoxelChunk.Size) + (cell.K * size) + dz) * VoxelChunk.Size + (cell.I * size);
                for (var dx = 0; dx < size; dx++)
                {
                    if (whole || LatticeGrid.Across(left, right, dx * IntMath.One / size) > _blobThresholds[row])
                    {
                        pick[at + dx] = (sbyte)row;
                        marked = true;
                    }
                }
            }
        }

        return marked;
    }

    private BlockId BaseRock(int x, int y, int z)
    {
        if (y >= _spec.DitherToY)
        {
            return _stone;
        }

        if (y < _spec.DitherFromY)
        {
            return _deep;
        }

        // Between the two: the share of deep rock falls in a straight line, drawn cell by cell from a hash of the position.
        var span = _spec.DitherToY - _spec.DitherFromY;
        var hash = WorldRng.Mix(_key ^ ((ulong)(uint)x * GoldenX) ^ ((ulong)(uint)y * GoldenY) ^ ((ulong)(uint)z * GoldenZ));
        return (int)(hash % (ulong)span) < _spec.DitherToY - y ? _deep : _stone;
    }
}
