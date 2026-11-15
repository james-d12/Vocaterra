namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// A pass that writes blocks into one chunk of a shaped column (B03): the ground's layers, the water, the stone, the trees, the undergrowth. Passes run in the fixed order the pipeline
/// registers, each seeing what the earlier ones wrote. Each has a unique <see cref="PassName"/> that is also the key of its random draws and noise. Implementations are immutable,
/// integer only and safe to call from any thread, and write only into the chunk they are given.
/// </summary>
internal interface IChunkDecorator
{
    /// <summary>The pass's name: unique, and different from every name the valley's builder uses.</summary>
    string PassName { get; }

    /// <summary>Writes the pass's blocks.</summary>
    /// <param name="ctx">The column of chunks, shaped.</param>
    /// <param name="chunk">The chunk being generated.</param>
    void Decorate(ColumnContext ctx, ChunkBuilder chunk);
}
