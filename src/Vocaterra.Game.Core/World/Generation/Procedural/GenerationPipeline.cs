namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The passes that turn a world's climate into blocks, in a fixed order registered in one place (B03, biomes plan section 3.2). The column shapers run once for a chunk column and settle the
/// heights: the biomes' surface strategies, the river banks, the edge of the water. The chunk decorators then write each chunk of the column: the ground's layers, the water, the stone, the
/// trees, the undergrowth. Every pass has a unique <see cref="IColumnShaper.PassName"/> or <see cref="IChunkDecorator.PassName"/>, which is also the key of its random draws and noise, so adding a
/// pass never reshuffles another. Later series add theirs here: the strata, ores, caves and lava of B11 go between the stone and the trees.
/// </summary>
internal sealed class GenerationPipeline
{
    public GenerationPipeline(IReadOnlyList<IColumnShaper> shapers, IReadOnlyList<IChunkDecorator> decorators)
    {
        Shapers = shapers;
        Decorators = decorators;
    }

    /// <summary>The column shapers, in the order they run.</summary>
    public IReadOnlyList<IColumnShaper> Shapers { get; }

    /// <summary>The chunk decorators, in the order they run.</summary>
    public IReadOnlyList<IChunkDecorator> Decorators { get; }

    /// <summary>Every pass's name, shapers first, in the order they run.</summary>
    public IReadOnlyList<string> PassNames => [.. Shapers.Select(static s => s.PassName), .. Decorators.Select(static d => d.PassName)];

    /// <summary>The order of the shipped passes: the one place that registers them.</summary>
    /// <param name="world">The world the passes read.</param>
    public static GenerationPipeline Standard(GenerationWorld world) => new(
        [new ReliefPass(world), new RiverBankPass(world), new WaterEdgePass(world)],
        [new SurfacePass(), new WaterPass(world), new StonePass(), new TreePass(), new UndergrowthPass(world)]);

    /// <summary>Runs the shapers over a chunk column's window.</summary>
    /// <param name="ctx">The column.</param>
    public void Shape(ColumnContext ctx)
    {
        foreach (var shaper in Shapers)
        {
            shaper.Shape(ctx);
        }
    }

    /// <summary>Runs the decorators into one chunk of a shaped column.</summary>
    /// <param name="ctx">The shaped column.</param>
    /// <param name="chunk">The chunk.</param>
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        foreach (var decorator in Decorators)
        {
            decorator.Decorate(ctx, chunk);
        }
    }
}
