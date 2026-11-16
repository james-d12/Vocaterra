using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>One vein of ore in one chunk: its row of the table, its block, and the chunk-local indices (<c>ChunkData</c> order) of the cells it tries to take.</summary>
/// <param name="Row">The ore's row in the table.</param>
/// <param name="Block">The ore's block.</param>
/// <param name="Cells">The chunk-local cell indices, each inside the chunk and inside the band's height range, no two the same.</param>
internal readonly record struct OreVein(int Row, BlockId Block, int[] Cells);

/// <summary>
/// The ores (<see cref="OreSpec"/> rows, B11, biomes plan section 3.5). In the manner of Minecraft 1.18 each band tries a number of veins in every chunk column, the height of a vein's centre drawn
/// from a triangular spread over the band's range that peaks at its own height; a chunk of the column takes the veins whose centres fall in its layer. A vein is a compact blob of the row's size
/// grown from its centre by random neighbours, kept inside the chunk (so no vein crosses its chunk's edge, which keeps generation chunk-local) and inside the band's range. The draws are
/// <c>WorldRng.For(seed, "ore:" + id, chunkX, chunkZ)</c> mixed with the layer, so a chunk's veins are a function of the seed and its coordinates alone. A vein replaces only the rocks its row lists: never air,
/// water, lava or another ore. Pure, immutable and safe from any thread.
/// </summary>
internal sealed class OreTable
{
    private const int Resolution = 1 << 20;
    private const ulong LayerKey = 0x9E3779B97F4A7C15UL;
    private const int GrowthTriesPerCell = 6;

    private static readonly (int X, int Y, int Z)[] Steps = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    private readonly OreSpec[] _rows;
    private readonly BlockId[] _blocks;
    private readonly bool[][] _replaces;
    private readonly ulong _seed;

    /// <summary>Creates the table of one world seed.</summary>
    /// <param name="ores">The rows.</param>
    /// <param name="seed">The world seed.</param>
    /// <exception cref="ArgumentException">A block name is not a block of the game.</exception>
    public OreTable(IReadOnlyList<OreSpec> ores, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(ores);
        _seed = seed;
        _rows = [.. ores];
        _blocks = [.. ores.Select(static o => UndergroundWorld.BlockNamed(o.Block))];
        _replaces = [.. ores.Select(static o =>
        {
            var flags = new bool[ushort.MaxValue + 1];
            foreach (var name in o.Replaces)
            {
                flags[(ushort)UndergroundWorld.BlockNamed(name)] = true;
            }

            return flags;
        })];
        MinY = _rows.Length == 0 ? int.MaxValue : _rows.Min(static o => o.Bands.Count == 0 ? int.MaxValue : o.Bands.Min(static b => b.MinY));
        MaxY = _rows.Length == 0 ? int.MinValue : _rows.Max(static o => o.Bands.Count == 0 ? int.MinValue : o.Bands.Max(static b => b.MaxY));
    }

    /// <summary>The rows.</summary>
    public IReadOnlyList<OreSpec> Rows => _rows;

    /// <summary>The lowest y any band reaches.</summary>
    public int MinY { get; }

    /// <summary>The highest y any band reaches.</summary>
    public int MaxY { get; }

    /// <summary>The block of a row.</summary>
    /// <param name="row">The row's index.</param>
    public BlockId BlockOf(int row) => _blocks[row];

    /// <summary>Whether a row's veins may replace the block.</summary>
    /// <param name="row">The row's index.</param>
    /// <param name="block">The block in the cell.</param>
    public bool Replaces(int row, BlockId block) => _replaces[row][(ushort)block];

    /// <summary>The veins of one chunk, in a fixed order (row by row, band by band, attempt by attempt).</summary>
    /// <param name="chunkX">The chunk's x.</param>
    /// <param name="chunkY">The chunk's layer.</param>
    /// <param name="chunkZ">The chunk's z.</param>
    public List<OreVein> Veins(int chunkX, int chunkY, int chunkZ)
    {
        var veins = new List<OreVein>();
        var minY = chunkY << VoxelChunk.Shift;
        var maxY = minY + VoxelChunk.Size - 1;
        for (var row = 0; row < _rows.Length; row++)
        {
            var spec = _rows[row];
            var column = WorldRng.For(_seed, "ore:" + spec.Id, chunkX, chunkZ).NextUInt64();
            for (var bandIndex = 0; bandIndex < spec.Bands.Count; bandIndex++)
            {
                var band = spec.Bands[bandIndex];
                if (band.MaxY < minY || band.MinY > maxY)
                {
                    continue;
                }

                var rng = new WorldRng(WorldRng.Mix(column ^ ((ulong)(uint)chunkY * LayerKey) ^ (ulong)bandIndex));
                for (var attempt = 0; attempt < band.Veins; attempt++)
                {
                    var y = CentreHeight(rng, band);
                    var size = rng.NextInt(spec.SizeMin, spec.SizeMax + 1);
                    var x = rng.NextInt(VoxelChunk.Size);
                    var z = rng.NextInt(VoxelChunk.Size);
                    if (y < minY || y > maxY)
                    {
                        continue;
                    }

                    var cells = Grow(rng, x, y - minY, z, size, Math.Max(band.MinY, minY) - minY, Math.Min(band.MaxY, maxY) - minY);
                    veins.Add(new OreVein(row, _blocks[row], cells));
                }
            }
        }

        return veins;
    }

    /// <summary>The height of a vein's centre: a triangular spread over the band's range that peaks at the band's peak.</summary>
    /// <param name="rng">The draws.</param>
    /// <param name="band">The band.</param>
    internal static int CentreHeight(WorldRng rng, OreBand band)
    {
        var range = band.MaxY - band.MinY;
        var up = band.PeakY - band.MinY;
        var down = band.MaxY - band.PeakY;
        var r = rng.NextInt(Resolution);
        var split = (int)((long)up * Resolution / range);
        return r < split
            ? band.MinY + (int)IntMath.ISqrt((long)r * range * up / Resolution)
            : band.MaxY - (int)IntMath.ISqrt((long)(Resolution - r) * range * down / Resolution);
    }

    // A compact blob: each new cell is a neighbour of a cell the vein already has, inside the chunk and between the band's lowest and highest layer (chunk-local).
    private static int[] Grow(WorldRng rng, int x, int y, int z, int size, int lowest, int highest)
    {
        var cells = new List<int>(size) { ChunkIndex(x, y, z) };
        var coords = new List<(int X, int Y, int Z)>(size) { (x, y, z) };
        for (var n = 1; n < size; n++)
        {
            for (var attempt = 0; attempt < GrowthTriesPerCell; attempt++)
            {
                var from = coords[rng.NextInt(coords.Count)];
                var step = Steps[rng.NextInt(Steps.Length)];
                var (nx, ny, nz) = (from.X + step.X, from.Y + step.Y, from.Z + step.Z);
                if ((uint)nx >= VoxelChunk.Size || (uint)nz >= VoxelChunk.Size || ny < lowest || ny > highest || cells.Contains(ChunkIndex(nx, ny, nz)))
                {
                    continue;
                }

                cells.Add(ChunkIndex(nx, ny, nz));
                coords.Add((nx, ny, nz));
                break;
            }
        }

        return [.. cells];
    }

    private static int ChunkIndex(int x, int y, int z) => (((y * VoxelChunk.Size) + z) * VoxelChunk.Size) + x;
}
