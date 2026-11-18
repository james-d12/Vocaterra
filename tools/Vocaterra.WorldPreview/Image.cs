namespace Vocaterra.WorldPreview;

/// <summary>A colour, 8 bits a channel.</summary>
internal readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>The colour with each channel multiplied by a factor (clamped to a byte).</summary>
    public Rgb Scaled(double factor) => new(Clamp(R * factor), Clamp(G * factor), Clamp(B * factor));

    /// <summary>The blend of two colours, 0 giving <paramref name="a"/> and 1 giving <paramref name="b"/>.</summary>
    public static Rgb Mix(Rgb a, Rgb b, double t) => new(Clamp(a.R + ((b.R - a.R) * t)), Clamp(a.G + ((b.G - a.G) * t)), Clamp(a.B + ((b.B - a.B) * t)));

    /// <summary>The colour as <c>#rrggbb</c>.</summary>
    public string Hex() => $"#{R:x2}{G:x2}{B:x2}";

    private static byte Clamp(double v) => (byte)Math.Clamp((int)Math.Round(v), 0, 255);
}

/// <summary>A raster of 8-bit RGB pixels, row by row, the origin at the top left.</summary>
internal sealed class Image
{
    /// <summary>Creates an image filled with a colour.</summary>
    public Image(int width, int height, Rgb background)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Pixels = new byte[checked(width * height * 3)];
        if (background != default)
        {
            for (var i = 0; i < width * height; i++)
            {
                Set(i % width, i / width, background);
            }
        }
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The bytes, three a pixel (red, green, blue).</summary>
    public byte[] Pixels { get; }

    /// <summary>Sets a pixel; one outside the image is ignored.</summary>
    public void Set(int x, int y, Rgb colour)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return;
        }

        var i = ((y * Width) + x) * 3;
        Pixels[i] = colour.R;
        Pixels[i + 1] = colour.G;
        Pixels[i + 2] = colour.B;
    }

    /// <summary>The pixel at (x, y).</summary>
    public Rgb Get(int x, int y)
    {
        var i = ((y * Width) + x) * 3;
        return new Rgb(Pixels[i], Pixels[i + 1], Pixels[i + 2]);
    }

    /// <summary>A vertical run of pixels from y0 to y1 inclusive, in either order.</summary>
    public void VerticalRun(int x, int y0, int y1, Rgb colour)
    {
        for (var y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
        {
            Set(x, y, colour);
        }
    }
}
