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

        return Layer(new ColumnContext(_world, coord.X, coord.Z), coord.Y, shaped: false);
    }

    /// <summary>
    /// The columns of a chunk column that open a cave entrance (B11): where the entrance mask has a zone on dry procedural land with a cave under it and a shaft is cut from the ground down to the cave.
    /// Empty for a source with no underground. The preview tool draws them.
    /// </summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    public IReadOnlyList<(int X, int Z)> EntranceColumns(int chunkX, int chunkZ)
    {
        var found = new List<(int X, int Z)>();
        if (_world.Underground is null)
        {
            return found;
        }

        var ctx = ShapedColumn(chunkX, chunkZ);
        var plan = ctx.CavePlan;
        for (var lz = 0; lz < VoxelChunk.Size; lz++)
        {
            for (var lx = 0; lx < VoxelChunk.Size; lx++)
            {
                if (plan.ShaftFrom[(lz * VoxelChunk.Size) + lx] != int.MaxValue)
                {
                    found.Add((ctx.ChunkMinX + lx, ctx.ChunkMinZ + lz));
                }
            }
        }

        return found;
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

    /// <summary>The chunk made by running every pass, with no shortcut for the chunks that are all air or all stone: the tests compare it with <see cref="Generate"/>.</summary>
    /// <param name="coord">The chunk's position.</param>
    internal ChunkData GenerateWithoutShortcuts(VoxelChunkCoord coord) => Layer(new ColumnContext(_world, coord.X, coord.Z), coord.Y, shaped: false, shortcuts: false);

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
            chunks[i] = Layer(ctx, MinChunkY + i, shaped: i > 0);
        }

        return chunks;
    }

    private ChunkData Layer(ColumnContext ctx, int chunkY, bool shaped, bool shortcuts = true)
    {
        var coord = new VoxelChunkCoord(ctx.ChunkX, chunkY, ctx.ChunkZ);
        if (!shaped)
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
