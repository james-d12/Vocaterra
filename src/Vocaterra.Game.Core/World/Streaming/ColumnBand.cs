namespace Vocaterra.Game.Core.World.Streaming;

/// <summary>
/// The chunk layers of one chunk column that meet the ground (B07, biomes plan section 3.7): the layers the generator does not shortcut as air above everything that can stand on the
/// ground (the ground, the sea, the tallest tree) or as solid stone under the deepest layers of the ground. Every layer outside it is, block for block, all air or all stone.
/// </summary>
/// <param name="MinLayer">The lowest chunk layer that can hold ground (inclusive).</param>
/// <param name="MaxLayer">The highest chunk layer that can hold anything (inclusive).</param>
public readonly record struct ColumnBand(int MinLayer, int MaxLayer)
{
    /// <summary>A column with no layer that meets the ground (everything is air or uniform stone).</summary>
    public static ColumnBand Empty { get; } = new(int.MaxValue, int.MinValue);

    /// <summary>Whether no layer meets the ground.</summary>
    public bool IsEmpty => MinLayer > MaxLayer;

    /// <summary>Whether a layer lies inside the band.</summary>
    /// <param name="layer">The chunk layer.</param>
    public bool Contains(int layer) => layer >= MinLayer && layer <= MaxLayer;

    /// <summary>How many layers the band holds.</summary>
    public int Count => IsEmpty ? 0 : MaxLayer - MinLayer + 1;
}
