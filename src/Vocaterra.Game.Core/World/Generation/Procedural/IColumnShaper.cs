namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// A pass that shapes the ground of a column of chunks (B03): it changes the heights the later passes build on, once for the whole column of 24 chunk layers and never a block. Each has a
/// unique <see cref="PassName"/> that is also the key of its random draws and noise. Implementations are immutable, integer only and safe to call from any thread.
/// </summary>
internal interface IColumnShaper
{
    /// <summary>The pass's name: unique, and different from every name the valley's builder uses.</summary>
    string PassName { get; }

    /// <summary>Changes the working heights of the context's window.</summary>
    /// <param name="ctx">The column of chunks and its neighbourhood.</param>
    void Shape(ColumnContext ctx);
}
