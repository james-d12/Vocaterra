using System.Collections.ObjectModel;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// What the passes learned while generating, kept for the guide ("where is the nearest tree"), for tests and for later passes. It is derived
/// data: it is not saved, and it describes the world as generated (mining does not update it).
/// </summary>
public sealed class GenerationRecord
{
    private readonly List<TilePoint> _treeBases = [];
    private readonly List<string> _passes = [];

    internal GenerationRecord(int width) => SurfaceY = new int[width];

    /// <summary>
    /// Per column, the row of the topmost solid ground tile as generated (the grass tile). Trees stand on it. Guaranteed to lie between
    /// <see cref="WorldGenerator.MinSurfaceY"/> and <see cref="WorldGenerator.MaxSurfaceY"/> for the world's height.
    /// </summary>
    internal int[] SurfaceY { get; }

    /// <summary>The surface row of every column (see <see cref="SurfaceY"/>).</summary>
    public IReadOnlyList<int> SurfaceHeights => new ReadOnlyCollection<int>(SurfaceY);

    /// <summary>The grass tile under every tree's trunk, in the order the forest pass placed them.</summary>
    public IReadOnlyList<TilePoint> TreeBases => _treeBases;

    /// <summary>The names of the passes that ran, in order.</summary>
    public IReadOnlyList<string> Passes => _passes;

    internal void AddTree(TilePoint grass) => _treeBases.Add(grass);

    internal void AddPass(string name) => _passes.Add(name);
}
