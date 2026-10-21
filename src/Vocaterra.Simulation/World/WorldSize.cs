using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation;

/// <summary>The size of a world in tiles. Both sides are positive multiples of the chunk size (32).</summary>
public readonly record struct WorldSize
{
    /// <summary>Creates a size.</summary>
    /// <exception cref="ArgumentException">A side is not a positive multiple of 32.</exception>
    public WorldSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || width % Chunk.Size != 0 || height % Chunk.Size != 0)
        {
            throw new ArgumentException($"The world size must be a positive multiple of {Chunk.Size} tiles in both directions.");
        }

        Width = width;
        Height = height;
    }

    /// <summary>The prototype's world: 1 024 x 512 tiles (32 x 16 chunks).</summary>
    public static WorldSize Small { get; } = new(1024, 512);

    /// <summary>The width in tiles.</summary>
    public int Width { get; }

    /// <summary>The height in tiles.</summary>
    public int Height { get; }
}
