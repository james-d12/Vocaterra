using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation;

/// <summary>Where a world came from: what a save stores to regenerate it.</summary>
/// <param name="Seed">The world seed; generation is a pure function of it.</param>
/// <param name="Size">The size in tiles.</param>
/// <param name="GeneratorVersion">The <see cref="WorldGenerator.Version"/> that made it.</param>
public sealed record WorldInfo(ulong Seed, WorldSize Size, int GeneratorVersion);

/// <summary>
/// A generated world and the read-only queries the front-end and the guide use: tiles, the spawn point and what generation recorded. Only
/// the simulation changes tiles.
/// </summary>
public sealed class World
{
    internal World(WorldInfo info, TileGrid tiles, TilePoint spawn, GenerationRecord record)
    {
        Info = info;
        Tiles = tiles;
        Spawn = spawn;
        Record = record;
    }

    /// <summary>The seed, size and generator version.</summary>
    public WorldInfo Info { get; }

    /// <summary>The tile grid (read access).</summary>
    public TileGrid Tiles { get; }

    /// <summary>
    /// The spawn tile: an air tile on the surface, with solid ground directly below it and clear air above it. A body standing there has
    /// its feet on the bottom edge of this tile.
    /// </summary>
    public TilePoint Spawn { get; }

    /// <summary>What the generation passes recorded (surface heights, tree bases, the pass names).</summary>
    public GenerationRecord Record { get; }

    /// <summary>The width in tiles.</summary>
    public int Width => Tiles.Width;

    /// <summary>The height in tiles.</summary>
    public int Height => Tiles.Height;

    /// <summary>The tile at the position; <see cref="TileId.Air"/> outside the world.</summary>
    public TileId GetTile(int x, int y) => Tiles.GetTile(x, y);

    /// <summary>Whether bodies collide at the position; everything outside the world is solid.</summary>
    public bool IsSolid(int x, int y) => Tiles.IsSolid(x, y);

    /// <summary>The SHA-256 of the tile arrays (see <see cref="TileGrid.ComputeDigest"/>) as 64 lowercase hex characters.</summary>
    public string ComputeDigestHex() => Convert.ToHexString(Tiles.ComputeDigest()).ToLowerInvariant();
}
