using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>What a generation pass sees: the seed, the grid being filled and the record. Passes live inside the simulation assembly.</summary>
internal sealed class GenerationContext
{
    public GenerationContext(ulong seed, WorldSize size)
    {
        Seed = seed;
        Size = size;
        Tiles = new TileGrid(size.Width, size.Height);
        Record = new GenerationRecord(size.Width);
    }

    public ulong Seed { get; }

    public WorldSize Size { get; }

    public TileGrid Tiles { get; }

    public GenerationRecord Record { get; }

    /// <summary>The spawn tile; set by the spawn pass.</summary>
    public TilePoint? Spawn { get; set; }

    /// <summary>The generator of one pass in one chunk (see <see cref="WorldRng.For"/>).</summary>
    public WorldRng Rng(string passName, int chunkX = 0, int chunkY = 0) => WorldRng.For(Seed, passName, chunkX, chunkY);

    /// <summary>
    /// A 64-bit seed for noise functions, derived from the world seed and a pass-local name, so every noise field is independent of the others.
    /// </summary>
    public ulong NoiseSeed(string name) => WorldRng.For(Seed, "noise:" + name).NextUInt64();
}
