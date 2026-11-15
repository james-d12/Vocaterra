using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>What the surface passes need of a column: the blocks of its layers, resolved from its biome and its final height.</summary>
/// <param name="Height">The y of the topmost solid block.</param>
/// <param name="Top">The top layer's block.</param>
/// <param name="Filler">The block of the layers under it.</param>
/// <param name="FillerDepth">How many filler layers lie under the top layer.</param>
/// <param name="Water">Whether the column lies under the sea (it fills with water to the sea level).</param>
internal readonly record struct ColumnSurface(int Height, BlockId Top, BlockId Filler, int FillerDepth, bool Water);

/// <summary>
/// A chunk column and its neighbourhood (B03): the window of 16 + 2 x margin columns on a side, with the climate and the height model's shape of every column, the heights the passes shape,
/// and the biomes classified on demand. Every pass reads this and nothing outside it, so what a chunk holds depends on the window and never on which chunks were made before. One context
/// serves all 24 chunk layers of the column; it is not shared between threads.
/// </summary>
internal sealed class ColumnContext
{
    private readonly ClimateSample[] _climate;
    private readonly ColumnShape[] _shapes;
    private readonly int[] _heights;
    private readonly BiomeColumn[] _biomes;
    private readonly bool[] _classified;
    private readonly ColumnSurface[] _surfaces;
    private readonly bool[] _surfaced;
    private readonly Lazy<List<PlantedTree>> _trees;

    public ColumnContext(GenerationWorld world, int chunkX, int chunkZ)
    {
        World = world;
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        Size = VoxelChunk.Size + (2 * world.Margin);
        OriginX = (chunkX << VoxelChunk.Shift) - world.Margin;
        OriginZ = (chunkZ << VoxelChunk.Shift) - world.Margin;
        var count = Size * Size;
        _climate = new ClimateSample[count];
        _shapes = new ColumnShape[count];
        _heights = new int[count];
        _biomes = new BiomeColumn[count];
        _classified = new bool[count];
        _surfaces = new ColumnSurface[count];
        _surfaced = new bool[count];
        _trees = new Lazy<List<PlantedTree>>(() => TreePass.Plan(this));
        world.Climate.Fill(OriginX, OriginZ, Size, Size, _climate);
        for (var j = 0; j < Size; j++)
        {
            for (var i = 0; i < Size; i++)
            {
                var k = (j * Size) + i;
                _shapes[k] = world.Height.Shape(OriginX + i, OriginZ + j, _climate[k]);
                _heights[k] = _shapes[k].Height;
            }
        }
    }

    /// <summary>The world being generated.</summary>
    public GenerationWorld World { get; }

    /// <summary>The chunk column's x.</summary>
    public int ChunkX { get; }

    /// <summary>The chunk column's z.</summary>
    public int ChunkZ { get; }

    /// <summary>The window's width in columns.</summary>
    public int Size { get; }

    /// <summary>The x of the window's first column.</summary>
    public int OriginX { get; }

    /// <summary>The z of the window's first column.</summary>
    public int OriginZ { get; }

    /// <summary>The x of the chunk column's first block.</summary>
    public int ChunkMinX => ChunkX << VoxelChunk.Shift;

    /// <summary>The z of the chunk column's first block.</summary>
    public int ChunkMinZ => ChunkZ << VoxelChunk.Shift;

    /// <summary>Whether the world column is inside the window.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public bool InWindow(int x, int z) => (uint)(x - OriginX) < (uint)Size && (uint)(z - OriginZ) < (uint)Size;

    /// <summary>The index of a world column in the window's arrays.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int Index(int x, int z) => ((z - OriginZ) * Size) + (x - OriginX);

    /// <summary>The climate of a column of the window.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public ClimateSample ClimateAt(int x, int z) => _climate[Index(x, z)];

    /// <summary>What the height model made of a column of the window.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public ColumnShape ShapeAt(int x, int z) => _shapes[Index(x, z)];

    /// <summary>The working height of a column: the topmost solid block as the passes so far have shaped it.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int HeightAt(int x, int z) => _heights[Index(x, z)];

    /// <summary>Sets the working height of a column.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="height">The height.</param>
    public void SetHeight(int x, int z, int height) => _heights[Index(x, z)] = height;

    /// <summary>The working heights of the whole window, row by row (x fastest): a shaper changes them in place.</summary>
    public int[] Heights => _heights;

    /// <summary>The biome of a column, chosen by the height model's ground (never by what a later pass did to it) and cached.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public BiomeColumn BiomeAt(int x, int z)
    {
        var k = Index(x, z);
        if (!_classified[k])
        {
            _biomes[k] = World.Table.Classify(_climate[k], _shapes[k].Height, x, z);
            _classified[k] = true;
        }

        return _biomes[k];
    }

    /// <summary>The layers of a column after every shaper has run (cached).</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public ColumnSurface SurfaceAt(int x, int z)
    {
        var k = Index(x, z);
        if (!_surfaced[k])
        {
            var biome = BiomeAt(x, z);
            var blocks = World.Table.Surface(biome.Row);
            var height = _heights[k];
            var water = height < World.SeaLevel;
            var top = water ? blocks.Underwater : biome.Snow ? BlockId.Snow : blocks.Top;
            _surfaces[k] = new ColumnSurface(height, top, blocks.Filler, blocks.FillerDepth, water);
            _surfaced[k] = true;
        }

        return _surfaces[k];
    }

    /// <summary>The trees whose crowns can reach this chunk column, planned once from the shaped ground (see <see cref="TreePass"/>), in the order they write their leaves.</summary>
    public IReadOnlyList<PlantedTree> Trees => _trees.Value;

    /// <summary>The highest working height among the chunk column's own 16 x 16 columns.</summary>
    public int HighestOwn() => Extreme(own: true, highest: true);

    /// <summary>The lowest working height among the chunk column's own 16 x 16 columns.</summary>
    public int LowestOwn() => Extreme(own: true, highest: false);

    /// <summary>The highest working height in the whole window.</summary>
    public int HighestInWindow() => Extreme(own: false, highest: true);

    private int Extreme(bool own, bool highest)
    {
        var margin = own ? World.Margin : 0;
        var result = highest ? int.MinValue : int.MaxValue;
        for (var j = margin; j < Size - margin; j++)
        {
            for (var i = margin; i < Size - margin; i++)
            {
                var h = _heights[(j * Size) + i];
                result = highest ? Math.Max(result, h) : Math.Min(result, h);
            }
        }

        return result;
    }
}
