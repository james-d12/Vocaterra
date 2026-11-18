using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// A copy of everything the mesher reads to build one chunk (B21): the chunk and the layer of cells round it (the 18 x 18 x 18 block the corner shading, the water's corners and the faces on the edges look at), the two layers
/// under the chunk's own columns (the water's depth reads three cells down), the blocks the lanterns that reach the chunk stand on (the glow's torch test) and the river's currents. It is taken on the game loop's thread, in
/// a few slab copies of the resident chunks' arrays and never by a read that could generate a chunk, and read afterwards by a worker, so a worker touches no mutable game state: a mesh built from a snapshot is the mesh built
/// from the grid at the moment the snapshot was taken. A cell the mesher never reads reads as air.
/// </summary>
public sealed class ChunkSnapshot : IBlockSource
{
    private const int Size = VoxelChunk.Size;
    private const int Edge = Size + 2;
    private const int Under = 2; // the layers y0 - 2 and y0 - 3 of the chunk's own columns (the layer y0 - 1 is in the 18-block)

    private readonly BlockId[] _blocks = new BlockId[Edge * Edge * Edge];
    private readonly byte[] _levels = new byte[Edge * Edge * Edge];
    private readonly BlockId[] _under = new BlockId[Under * Size * Size];
    private readonly VoxelChunk?[] _around = new VoxelChunk?[27];
    private readonly Dictionary<BlockPos, BlockId> _extras = [];
    private readonly Reader _reader;
    private int _x0;
    private int _y0;
    private int _z0;

    /// <summary>Makes an empty snapshot (reused through <see cref="Fill"/>).</summary>
    public ChunkSnapshot() => _reader = new Reader(this);

    /// <summary>The chunk the snapshot is of.</summary>
    public VoxelChunkCoord Coord { get; private set; }

    /// <summary>How many blocks of the chunk are not air (what <see cref="ChunkMesher.IsBuried(IBlockSource, VoxelChunkCoord, int, UInt128)"/> asks).</summary>
    public int NonAirCount { get; private set; }

    /// <summary>The block kinds the chunk holds, one bit each.</summary>
    public UInt128 KindMask { get; private set; }

    /// <summary>
    /// Whether the chunk's whole neighbourhood is there: on a streamed grid every one of its 26 neighbours inside the world is resident (a chunk that is not reads as air, which would draw a wall at the seam of a half-loaded world), always on a
    /// bounded grid. The view set lists only such chunks; the residency may have changed since, so the pipeline asks again when it takes the snapshot.
    /// </summary>
    public bool Complete { get; private set; }

    /// <summary>The river's currents, or <see langword="null"/> for none (immutable, so shared).</summary>
    public RiverCurrents? Currents { get; private set; }

    /// <summary>Takes a snapshot of a resident chunk and what round it the mesher reads (a chunk that is not resident reads as air, as the grid's peek does; the floor under the world reads as stone).</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="coord">The chunk (resident).</param>
    /// <param name="currents">The river's currents, or <see langword="null"/>.</param>
    /// <param name="lights">The lights near the chunk (<see cref="ChunkLightIndex.Near"/>): the blocks they stand on are kept, or <see langword="null"/>.</param>
    /// <returns>The snapshot, or <see langword="null"/> when the chunk is not resident.</returns>
    public static ChunkSnapshot? Capture(VoxelGrid grid, VoxelChunkCoord coord, RiverCurrents? currents = null, IReadOnlyList<BlockPos>? lights = null)
    {
        var snapshot = new ChunkSnapshot();
        return snapshot.Fill(grid, coord, currents, lights) ? snapshot : null;
    }

    /// <summary>Fills this snapshot (it may have held another chunk before) from the grid.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="coord">The chunk.</param>
    /// <param name="currents">The river's currents, or <see langword="null"/>.</param>
    /// <param name="lights">The lights near the chunk, or <see langword="null"/>.</param>
    /// <returns>Whether the chunk is resident (false leaves the snapshot unusable).</returns>
    public bool Fill(VoxelGrid grid, VoxelChunkCoord coord, RiverCurrents? currents = null, IReadOnlyList<BlockPos>? lights = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (!grid.TryGetChunk(coord, out var self))
        {
            return false;
        }

        Coord = coord;
        Currents = currents;
        (_x0, _y0, _z0) = (coord.X * Size, coord.Y * Size, coord.Z * Size);
        NonAirCount = self.NonAirCount;
        KindMask = self.KindMask;
        var i = 0;
        Complete = true;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 && dz == 0)
                    {
                        _around[i++] = self;
                        continue;
                    }

                    var next = new VoxelChunkCoord(coord.X + dx, coord.Y + dy, coord.Z + dz);
                    var found = grid.TryGetChunk(next, out var neighbour);
                    if (!found && grid.IsStreamed && grid.Limits.Contains(next))
                    {
                        Complete = false;
                    }

                    _around[i++] = found && !neighbour!.IsEmpty ? neighbour : null;
                }
            }
        }

        var minY = grid.Limits.MinY;
        for (var j = 0; j < Edge; j++)
        {
            var layer = j * Edge * Edge;
            if (_y0 - 1 + j < minY)
            {
                Array.Fill(_blocks, BlockId.Stone, layer, Edge * Edge); // the floor under the world reads as stone, so the underside of the world is not meshed
                Array.Clear(_levels, layer, Edge * Edge);
                continue;
            }

            var cy = j == 0 ? 0 : j == Edge - 1 ? 2 : 1;
            var ly = j == 0 ? Size - 1 : j == Edge - 1 ? 0 : j - 1;
            for (var k = 0; k < Edge; k++)
            {
                var cz = k == 0 ? 0 : k == Edge - 1 ? 2 : 1;
                var lz = k == 0 ? Size - 1 : k == Edge - 1 ? 0 : k - 1;
                var row = layer + (k * Edge);
                var west = _around[(((cy * 3) + cz) * 3) + 0];
                var middle = _around[(((cy * 3) + cz) * 3) + 1];
                var east = _around[(((cy * 3) + cz) * 3) + 2];
                var source = ((ly * Size) + lz) * Size;
                CopyCell(west, source + (Size - 1), row);
                if (middle is null)
                {
                    Array.Clear(_blocks, row + 1, Size);
                    Array.Clear(_levels, row + 1, Size);
                }
                else
                {
                    middle.Blocks.Slice(source, Size).CopyTo(_blocks.AsSpan(row + 1, Size));
                    if (middle.HasWaterLevels)
                    {
                        middle.WaterLevels.Slice(source, Size).CopyTo(_levels.AsSpan(row + 1, Size));
                    }
                    else
                    {
                        Array.Clear(_levels, row + 1, Size);
                    }
                }

                CopyCell(east, source, row + Edge - 1);
            }
        }

        // The two layers under the chunk's own columns: the water's depth reads down to three cells.
        var below = _around[4];
        for (var d = 0; d < Under; d++)
        {
            var offset = d * Size * Size;
            if (_y0 - 2 - d < minY)
            {
                Array.Fill(_under, BlockId.Stone, offset, Size * Size);
            }
            else if (below is null)
            {
                Array.Clear(_under, offset, Size * Size);
            }
            else
            {
                below.Blocks.Slice((Size - 2 - d) * Size * Size, Size * Size).CopyTo(_under.AsSpan(offset, Size * Size));
            }
        }

        _extras.Clear();
        if (lights is not null)
        {
            for (var n = 0; n < lights.Count; n++)
            {
                var light = lights[n];
                if (light.X + 0.5f >= _x0 - ChunkMesher.GlowRadius && light.X + 0.5f <= _x0 + Size + ChunkMesher.GlowRadius
                    && light.Y + 0.5f >= _y0 - ChunkMesher.GlowRadius && light.Y + 0.5f <= _y0 + Size + ChunkMesher.GlowRadius
                    && light.Z + 0.5f >= _z0 - ChunkMesher.GlowRadius && light.Z + 0.5f <= _z0 + Size + ChunkMesher.GlowRadius)
                {
                    _extras[light] = light.Y < minY ? BlockId.Stone : grid.PeekBlock(light.X, light.Y, light.Z);
                }
            }
        }

        Array.Clear(_around); // the chunks stay with the game loop: the snapshot keeps no reference to one
        return true;
    }

    private void CopyCell(VoxelChunk? chunk, int index, int destination)
    {
        if (chunk is null)
        {
            _blocks[destination] = BlockId.Air;
            _levels[destination] = 0;
            return;
        }

        _blocks[destination] = chunk.Blocks[index];
        _levels[destination] = chunk.HasWaterLevels ? chunk.WaterLevels[index] : (byte)0;
    }

    /// <inheritdoc />
    public BlockId Get(int x, int y, int z)
    {
        int lx = x - _x0, ly = y - _y0, lz = z - _z0;
        if ((uint)(lx + 1) < Edge && (uint)(ly + 1) < Edge && (uint)(lz + 1) < Edge)
        {
            return _blocks[((((ly + 1) * Edge) + (lz + 1)) * Edge) + lx + 1];
        }

        if ((uint)lx < Size && (uint)lz < Size && (ly == -2 || ly == -3))
        {
            return _under[(((((-ly) - 2) * Size) + lz) * Size) + lx];
        }

        return _extras.Count > 0 && _extras.TryGetValue(new BlockPos(x, y, z), out var id) ? id : BlockId.Air;
    }

    /// <inheritdoc />
    public WaterLevel GetWater(int x, int y, int z)
    {
        int lx = x - _x0, ly = y - _y0, lz = z - _z0;
        return (uint)(lx + 1) < Edge && (uint)(ly + 1) < Edge && (uint)(lz + 1) < Edge
            ? WaterLevel.FromEncoded(_levels[((((ly + 1) * Edge) + (lz + 1)) * Edge) + lx + 1])
            : WaterLevel.Source;
    }

    /// <inheritdoc />
    public FlowVector FlowAt(int x, int y, int z)
    {
        var flow = FlowVector.At(_reader, x, y, z);
        if (flow.IsHorizontal || Currents is null || Get(x, y, z) != BlockId.Water || !GetWater(x, y, z).IsSource)
        {
            return flow;
        }

        return Currents.At(x, z);
    }

    // The snapshot as the flow reads it: a block and a level, nothing else.
    private sealed class Reader(ChunkSnapshot snapshot) : IVoxelReader
    {
        public WorldLimits Limits => throw new NotSupportedException("The mesher's snapshot has no limits to read.");

        public bool InBounds(int x, int y, int z) => throw new NotSupportedException("The flow does not ask where the world ends.");

        public BlockId GetBlock(int x, int y, int z) => snapshot.Get(x, y, z);

        public WaterLevel GetWater(int x, int y, int z) => snapshot.GetWater(x, y, z);

        public bool IsSolid(int x, int y, int z) => throw new NotSupportedException("The flow does not ask whether a cell is solid.");

        public bool BlocksBody(int x, int y, int z) => throw new NotSupportedException("The flow does not ask whether a cell blocks a body.");

        public bool CanStandOn(int x, int y, int z) => throw new NotSupportedException("The flow does not ask where a body can stand.");

        public int HighestBlock(int x, int z) => throw new NotSupportedException("The flow does not ask for the highest block of a column.");
    }
}
