using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// Placeholder tile art drawn in code (D-game-5: no third-party art yet): flat colours with a deterministic speckle, as RGBA bytes. The
/// engine layer uploads these into a texture; P4 replaces them with a real atlas and its grey copy. Transparent pixels have alpha 0.
/// </summary>
public static class TileArt
{
    /// <summary>The width and height of one tile's art in pixels.</summary>
    public const int Size = 16;

    /// <summary>The number of tile ids the atlas holds (ids are consecutive from <see cref="TileId.Air"/>).</summary>
    public static int TileCount => TileDefinition.All.Count;

    private static readonly (byte R, byte G, byte B, byte A) Transparent = (0, 0, 0, 0);

    /// <summary>The RGBA bytes (row by row, <see cref="Size"/> x <see cref="Size"/> x 4) of the art for <paramref name="id"/>.</summary>
    public static byte[] Pixels(TileId id)
    {
        var data = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var (r, g, b, a) = Pixel(id, x, y);
                var i = ((y * Size) + x) * 4;
                data[i] = r;
                data[i + 1] = g;
                data[i + 2] = b;
                data[i + 3] = a;
            }
        }

        return data;
    }

    private static (byte R, byte G, byte B, byte A) Pixel(TileId id, int x, int y)
    {
        var noise = Mix((uint)id, (uint)x, (uint)y) % 5; // 0..4: a small, deterministic speckle
        switch (id)
        {
            case TileId.Dirt:
                return Shade(120, 80, 48, noise);
            case TileId.Grass:
                return y < 4 ? Shade(78, 160, 56, noise) : Shade(120, 80, 48, noise);
            case TileId.Stone:
                return Shade(120, 124, 132, noise);
            case TileId.Trunk:
                return x is >= 5 and <= 10 ? Shade(102, 66, 34, noise) : Transparent;
            case TileId.Leaves:
                return Mix(7, (uint)x, (uint)y) % 6 == 0 ? Transparent : Shade(46, 128, 52, noise);
            default:
                return Transparent;
        }
    }

    private static (byte R, byte G, byte B, byte A) Shade(int r, int g, int b, uint noise)
    {
        var d = (int)noise * 5;
        return ((byte)(r + d), (byte)(g + d), (byte)(b + d), 255);
    }

    /// <summary>An integer hash of three values (a murmur-style finaliser), so the speckle needs neither a random generator nor the clock.</summary>
    private static uint Mix(uint a, uint b, uint c)
    {
        var h = unchecked((a * 0x9E3779B1) ^ (b * 0x85EBCA6B) ^ (c * 0xC2B2AE35));
        h ^= h >> 15;
        h = unchecked(h * 0x2C1B3C6D);
        h ^= h >> 12;
        h = unchecked(h * 0x297A2D39);
        return h ^ (h >> 15);
    }
}
