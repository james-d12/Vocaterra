using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The procedural world's chunks (B03, D-game-34): <see cref="IChunkSource"/> over the climate, the height model, the biome table and the <see cref="GenerationPipeline"/>. A chunk is a pure
/// function of the seed, the data, the strategies and its coordinates: the source holds nothing that changes after construction, makes everything it needs for a call inside the call, and is
/// safe to call from any thread, in any order, any number of times. The chunk's climate is filled once for the whole window (a chunk column and the margin its neighbours reach in by), then
/// the pipeline's shapers settle the heights and its decorators write the blocks. Between the sea floor and the sky the world is 24 chunk layers; a chunk outside that range is air.
/// </summary>
public sealed class ProceduralChunkSource : IChunkSource
{
    private readonly GenerationWorld _world;
    private readonly GenerationPipeline _pipeline;

    /// <summary>Creates the source of one world seed.</summary>
    /// <param name="world">The world's numbers.</param>
    /// <param name="biomes">The biome rows.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="strategies">The code strategies the rows may name; <see cref="StrategyRegistry.Default"/> when <see langword="null"/>.</param>
    /// <param name="underground">The underground (rock layers, floor, ores, caves, lava), or <see langword="null"/> for a ground of plain stone (B11).</param>
    /// <exception cref="ArgumentException">A biome row names a block the game does not have, or a strategy, species or crown the registry does not have.</exception>
    public ProceduralChunkSource(WorldGenData world, BiomeData biomes, ulong seed, StrategyRegistry? strategies = null, UndergroundData? underground = null)
        : this(world, biomes, seed, strategies, 0, null, underground)
    {
    }

    /// <summary>A source whose window is wider than it needs to be by <paramref name="extraMargin"/> columns: the tests use it to show that the margin is enough, because a chunk must come out the same however wide the window.</summary>
    internal ProceduralChunkSource(WorldGenData world, BiomeData biomes, ulong seed, StrategyRegistry? strategies, int extraMargin, UndergroundData? underground = null)
        : this(world, biomes, seed, strategies, extraMargin, null, underground)
    {
    }

    /// <summary>A source whose ground is eased into the edges of authored regions (B06): the composite source's procedural half.</summary>
    internal ProceduralChunkSource(WorldGenData world, BiomeData biomes, ulong seed, StrategyRegistry? strategies, int extraMargin, RegionBlend? blend, UndergroundData? underground = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(biomes);
        _world = new GenerationWorld(world, biomes, seed, strategies ?? StrategyRegistry.Default, extraMargin, blend, underground);
        _pipeline = GenerationPipeline.Standard(_world);
    }

    /// <summary>The world being generated (climate, height model, biome table), which the place query reads.</summary>
    internal GenerationWorld World => _world;

    /// <summary>The lowest chunk layer of the world (y -64 is layer -4).</summary>
    public int MinChunkY => _world.MinY >> VoxelChunk.Shift;

    /// <summary>One above the highest chunk layer (y 320 is the top of layer 19).</summary>
    public int MaxChunkY => _world.MaxY >> VoxelChunk.Shift;

    /// <summary>The passes, in the order they run (for the tests).</summary>
    internal GenerationPipeline Pipeline => _pipeline;

    /// <summary>How many columns the window reaches beyond a chunk (for the tests).</summary>
    internal int Margin => _world.Margin;

    /// <inheritdoc />
    public ChunkData Generate(VoxelChunkCoord coord)
    {
        if (coord.Y < MinChunkY || coord.Y >= MaxChunkY)
        {
            return ChunkData.Empty(coord);
        }

        return Layer(new ColumnContext(_world, coord.X, coord.Z), coord.Y);
    }

    /// <summary>
    /// The mouths of the cave entrances in a chunk column (B11b): the column of each passage's mouth (its first block along) that lies in the chunk column. Empty for a source with no underground. The preview
    /// tool draws them.
    /// </summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    public IReadOnlyList<(int X, int Z)> EntranceColumns(int chunkX, int chunkZ) => [.. Ramps(chunkX, chunkZ).Select(static r => (r.AnchorX, r.AnchorZ))];

    /// <summary>The cave entrances whose mouth lies in a chunk column, with their direction and depth (B11b): what the preview tool lists and draws. Empty for a source with no underground.</summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    public IReadOnlyList<CaveEntrance> CaveEntrances(int chunkX, int chunkZ) =>
        [.. Ramps(chunkX, chunkZ).Select(r => new CaveEntrance(r.AnchorX, r.AnchorZ, r.MouthY, r.Length, r.Landing, r.MouthY - r.FloorAt(r.Length), GroundOver(r, 12), [.. r.Nodes.Select(static n => (n.X, n.Z))], [.. r.Nodes.Select(static n => n.Floor)]))];

    // How far the model's ground lies over the passage's floor at an index along.
    private int GroundOver(EntranceRamp ramp, int along)
    {
        var (x, z) = ramp.CentreAt(along);
        return _world.Height.Shape(x, z, _world.Climate.At(x, z)).Height - ramp.FloorAt(along);
    }

    /// <summary>The passages whose mouth lies in a chunk column (for the tests and the preview).</summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    internal IReadOnlyList<EntranceRamp> Ramps(int chunkX, int chunkZ)
    {
        if (_world.Entrances is not { } sites)
        {
            return [];
        }

        var (minX, minZ) = (chunkX << VoxelChunk.Shift, chunkZ << VoxelChunk.Shift);
        return [.. sites.Near(minX, minZ, minX + VoxelChunk.Size - 1, minZ + VoxelChunk.Size - 1).Where(r => r.AnchorX >= minX && r.AnchorX < minX + VoxelChunk.Size && r.AnchorZ >= minZ && r.AnchorZ < minZ + VoxelChunk.Size)];
    }

    /// <summary>A chunk column's window after the shapers have run: its final heights and biomes, which the tests read to check the ground (the blocks of a chunk also hold the trees).</summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    internal ColumnContext ShapedColumn(int chunkX, int chunkZ)
    {
        var ctx = new ColumnContext(_world, chunkX, chunkZ);
        _pipeline.Shape(ctx);
        return ctx;
    }

    /// <summary>
    /// The chunk layers of a chunk column that the generator does not shortcut (B07): the shaped ground decides, exactly as <see cref="Layer"/> does. Chunks above the band are air, chunks under it are
    /// stone; nothing is decorated to find out, so it costs the window's heights and the shapers, not the passes.
    /// </summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    internal ColumnBand SurfaceBand(int chunkX, int chunkZ)
    {
        var ctx = ShapedColumn(chunkX, chunkZ);
        return BandOf(ctx.HighestInWindow(), ctx.LowestOwn());
    }

    /// <summary>The same band, from the shaped window of a group that holds the column (B39): no window of its own is made.</summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    /// <param name="group">The group's shaped window; the column must belong to it.</param>
    internal ColumnBand SurfaceBand(int chunkX, int chunkZ, ColumnGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.Contains(chunkX, chunkZ) ? BandOf(group.HighestInWindowOf(chunkX, chunkZ), group.LowestOwnOf(chunkX, chunkZ)) : SurfaceBand(chunkX, chunkZ);
    }

    private ColumnBand BandOf(int highestInWindow, int lowestOwn)
    {
        var top = Math.Max(highestInWindow, _world.SeaLevel) + _world.Strategies.MaxTreeHeight;
        var bottom = lowestOwn - _world.MaxFillerDepth - 1;
        var min = Math.Max(bottom >> VoxelChunk.Shift, MinChunkY);
        var max = Math.Min(top >> VoxelChunk.Shift, MaxChunkY - 1);
        return min > max ? ColumnBand.Empty : new ColumnBand(min, max);
    }

    /// <summary>
    /// The shaped window of a square of chunk columns, made once for all of them (B39): the climate, the height model and the shapers run over the square and the margin round it, and each column of the square
    /// then cuts its own window out of it, for its band (<see cref="SurfaceBand(int, int, ColumnGroup)"/>) and its layers (<see cref="GenerateLayers(int, int, int, int, ColumnGroup?)"/>).
    /// </summary>
    /// <param name="minChunkX">The first chunk column's x.</param>
    /// <param name="minChunkZ">The first chunk column's z.</param>
    /// <param name="span">How many chunk columns the square spans on a side (1 for a column alone).</param>
    public ColumnGroup ShapeGroup(int minChunkX, int minChunkZ, int span)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(span, 1);
        var ctx = new ColumnContext(_world, minChunkX, minChunkZ, span);
        _pipeline.Shape(ctx);
        return new ColumnGroup(ctx, minChunkX, minChunkZ, span);
    }

    /// <summary>The chunk made by running every pass, with no shortcut for the chunks that are all air or all stone: the tests compare it with <see cref="Generate"/>.</summary>
    /// <param name="coord">The chunk's position.</param>
    internal ChunkData GenerateWithoutShortcuts(VoxelChunkCoord coord) => Layer(new ColumnContext(_world, coord.X, coord.Z), coord.Y, shortcuts: false);

    /// <summary>
    /// Every chunk of a chunk column, from the bottom layer up, sharing one window: the cheap way to make many layers of one column, since the climate, the height model and the biomes are
    /// computed once. Each chunk equals what <see cref="Generate"/> gives for its coordinates.
    /// </summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    public IReadOnlyList<ChunkData> GenerateColumn(int chunkX, int chunkZ)
    {
        var ctx = new ColumnContext(_world, chunkX, chunkZ);
        var chunks = new ChunkData[MaxChunkY - MinChunkY];
        for (var i = 0; i < chunks.Length; i++)
        {
            chunks[i] = Layer(ctx, MinChunkY + i);
        }

        return chunks;
    }

    /// <summary>
    /// Some layers of a chunk column, from the lowest asked for up, sharing one window (B07): what a streamed world needs of a column is its surface band and a layer each side, and the layers under the
    /// ground cost most (the caves), so only the ones asked for are decorated. Each chunk equals what <see cref="Generate"/> gives for its coordinates. A layer outside the world is air.
    /// </summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    /// <param name="minLayer">The lowest layer asked for (inclusive).</param>
    /// <param name="maxLayer">The highest layer asked for (inclusive).</param>
    public IReadOnlyList<ChunkData> GenerateLayers(int chunkX, int chunkZ, int minLayer, int maxLayer) => GenerateLayers(chunkX, chunkZ, minLayer, maxLayer, null);

    /// <summary>
    /// The same layers, from the shaped window of a group that holds the column when one is given (B39): the column's window is cut from it and not made again. A column the group does not hold is made from its own window.
    /// </summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    /// <param name="minLayer">The lowest layer asked for (inclusive).</param>
    /// <param name="maxLayer">The highest layer asked for (inclusive).</param>
    /// <param name="group">The group's shaped window, or <see langword="null"/>.</param>
    public IReadOnlyList<ChunkData> GenerateLayers(int chunkX, int chunkZ, int minLayer, int maxLayer, ColumnGroup? group) => GenerateLayers(chunkX, chunkZ, minLayer, maxLayer, group, null);

    /// <summary>Arrays for the window of one chunk column, to hand back to <see cref="GenerateLayers(int, int, int, int, ColumnGroup?, ColumnScratch?)"/> (B40); one for each thread that generates at a time.</summary>
    public ColumnScratch CreateScratch() => new(VoxelChunk.Size + (2 * _world.Margin));

    /// <summary>
    /// The same layers, with the column's window arrays taken from <paramref name="scratch"/> when it and a group are given (B40): no array of the window is allocated. The chunks are the same bytes. The scratch is the caller's, for this call alone.
    /// </summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    /// <param name="minLayer">The lowest layer asked for (inclusive).</param>
    /// <param name="maxLayer">The highest layer asked for (inclusive).</param>
    /// <param name="group">The group's shaped window, or <see langword="null"/>.</param>
    /// <param name="scratch">The caller's arrays (<see cref="CreateScratch"/>), or <see langword="null"/>.</param>
    public IReadOnlyList<ChunkData> GenerateLayers(int chunkX, int chunkZ, int minLayer, int maxLayer, ColumnGroup? group, ColumnScratch? scratch)
    {
        var first = Math.Max(minLayer, MinChunkY);
        var last = Math.Min(maxLayer, MaxChunkY - 1);
        if (last < first)
        {
            return [];
        }

        var ctx = group is not null && group.Contains(chunkX, chunkZ) ? new ColumnContext(_world, chunkX, chunkZ, group, scratch) : new ColumnContext(_world, chunkX, chunkZ);
        var chunks = new ChunkData[last - first + 1];
        for (var i = 0; i < chunks.Length; i++)
        {
            chunks[i] = Layer(ctx, first + i);
        }

        return chunks;
    }

    private ChunkData Layer(ColumnContext ctx, int chunkY, bool shortcuts = true)
    {
        var coord = new VoxelChunkCoord(ctx.ChunkX, chunkY, ctx.ChunkZ);
        if (!ctx.Shaped)
        {
            _pipeline.Shape(ctx);
        }

        var minY = chunkY << VoxelChunk.Shift;
        var maxY = minY + VoxelChunk.Size - 1;

        // Above everything that can stand over the ground (the ground, the sea, the tallest tree): air.
        if (shortcuts && minY > Math.Max(ctx.HighestInWindow(), _world.SeaLevel) + _world.Strategies.MaxTreeHeight)
        {
            return ChunkData.Empty(coord);
        }

        var builder = new ChunkBuilder(coord);

        // Deeper than the deepest layers of the ground: stone, whatever the biome (with no underground; with one the rock layers, ores and caves still have to run).
        if (shortcuts && _world.Underground is null && maxY < ctx.LowestOwn() - _world.MaxFillerDepth - 1)
        {
            builder.FillAll(BlockId.Stone);
            return builder.ToData();
        }

        _pipeline.Decorate(ctx, builder);
        return builder.ToData();
    }
}
