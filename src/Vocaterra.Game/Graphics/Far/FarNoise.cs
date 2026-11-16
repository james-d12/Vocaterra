using Microsoft.Xna.Framework.Graphics;

namespace Vocaterra.Game.Graphics.Far;

/// <summary>
/// The noise the far land is textured with (B07c; Distant Horizons calls it noise texturing): a small tile of grey values a little under white, one texel to a cell of the tile's level, wrapped over every face, so a field of cells that merged into one
/// large quad still has the cells' own small differences; further off the mips average it away, so it never shimmers. Its mean is <see cref="Mean"/> and the mesher's colours are brought up by <see cref="FarTileMesher.NoiseGain"/> to make up for it.
/// </summary>
public static class FarNoise
{
    /// <summary>The brightest and darkest a texel is, as a share of white.</summary>
    public const float Lightest = 1.0f;

    /// <summary>The darkest a texel is.</summary>
    public const float Darkest = 0.86f;

    /// <summary>The mean brightness of the texture.</summary>
    public const float Mean = (Lightest + Darkest) / 2f;

    /// <summary>How many mip levels the texture has.</summary>
    public static int Levels => (int)Math.Log2(FarTileMesher.NoiseSize) + 1;

    /// <summary>The texture's pixels at a mip level (RGBA bytes, grey), the top level made from a hash of the texel and each below it the mean of four above.</summary>
    /// <param name="level">The level, 0 the full size.</param>
    public static byte[] Pixels(int level)
    {
        var size = FarTileMesher.NoiseSize;
        var values = new float[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var h = ((uint)x * 0x9E3779B1u) ^ ((uint)y * 0x85EBCA6Bu) ^ 0x51ED270Bu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                values[(y * size) + x] = Darkest + ((Lightest - Darkest) * (h & 0xFFFF) / 65535f);
            }
        }

        for (var l = 0; l < level; l++)
        {
            var half = size / 2;
            var next = new float[half * half];
            for (var y = 0; y < half; y++)
            {
                for (var x = 0; x < half; x++)
                {
                    next[(y * half) + x] = (values[(2 * y * size) + (2 * x)] + values[(2 * y * size) + (2 * x) + 1] + values[(((2 * y) + 1) * size) + (2 * x)] + values[(((2 * y) + 1) * size) + (2 * x) + 1]) / 4f;
                }
            }

            values = next;
            size = half;
        }

        var pixels = new byte[size * size * 4];
        for (var i = 0; i < values.Length; i++)
        {
            var g = (byte)Math.Round(values[i] * 255f);
            pixels[i * 4] = g;
            pixels[(i * 4) + 1] = g;
            pixels[(i * 4) + 2] = g;
            pixels[(i * 4) + 3] = 255;
        }

        return pixels;
    }

    /// <summary>Makes the texture on a device: every mip level.</summary>
    /// <param name="device">The graphics device.</param>
    public static Texture2D Create(GraphicsDevice device)
    {
        var texture = new Texture2D(device, FarTileMesher.NoiseSize, FarTileMesher.NoiseSize, true, SurfaceFormat.Color);
        for (var level = 0; level < Math.Min(texture.LevelCount, Levels); level++)
        {
            var pixels = Pixels(level);
            texture.SetData(level, null, pixels, 0, pixels.Length);
        }

        return texture;
    }

    /// <summary>The sampler: points up close (a cell is a crisp block), the mips between, wrapping.</summary>
    public static SamplerState Sampler { get; } = new()
    {
        Filter = TextureFilter.MinLinearMagPointMipLinear,
        AddressU = TextureAddressMode.Wrap,
        AddressV = TextureAddressMode.Wrap,
    };
}
