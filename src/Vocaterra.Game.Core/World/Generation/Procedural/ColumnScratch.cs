using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The arrays of one chunk column's window (B40): the context of a column cut from a group's window takes them from the caller instead of allocating about 170 KB of new ones for each column, which is most of what the generation
/// allocates. One scratch serves one column at a time (not thread-safe); it holds no state a later column can read, because the context clears its flags. Owned by the caller, so nothing here is static.
/// </summary>
public sealed class ColumnScratch
{
    /// <summary>Makes the arrays for a window <paramref name="size"/> columns on a side (use <see cref="ProceduralChunkSource.CreateScratch"/>).</summary>
    /// <param name="size">The window's width.</param>
    public ColumnScratch(int size)
    {
        Count = size * size;
        Climate = new ClimateSample[Count];
        Shapes = new ColumnShape[Count];
        Heights = new int[Count];
        Wet = new int[Count];
        Biomes = new BiomeColumn[Count];
        Classified = new bool[Count];
        Surfaces = new ColumnSurface[Count];
        Surfaced = new bool[Count];
    }

    internal int Count { get; }

    internal ClimateSample[] Climate { get; }

    internal ColumnShape[] Shapes { get; }

    internal int[] Heights { get; }

    internal int[] Wet { get; }

    internal BiomeColumn[] Biomes { get; }

    internal bool[] Classified { get; }

    internal ColumnSurface[] Surfaces { get; }

    internal bool[] Surfaced { get; }
}
