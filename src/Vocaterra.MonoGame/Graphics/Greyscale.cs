namespace Vocaterra.MonoGame.Graphics;

/// <summary>
/// The greyscale copy of an RGBA picture, made on the CPU (the 2D slice did the same at load, research section 7.3): each pixel's colour becomes
/// its luma and its alpha is kept. Integer weights (299, 587, 114 over 1000, the Rec. 601 luma) so the result is the same on every machine and no
/// shader is needed. The 3D world uses it for the grey copy of each block cell (<see cref="BlockArt"/>), behind <c>--greyscale</c>.
/// </summary>
public static class Greyscale
{
    /// <summary>The luma of one colour, 0 to 255: <c>0.299 R + 0.587 G + 0.114 B</c>, rounded.</summary>
    public static byte Luma(byte r, byte g, byte b) => (byte)((299 * r + 587 * g + 114 * b + 500) / 1000);

    /// <summary>
    /// Returns a new RGBA buffer, the same size as <paramref name="rgba"/>, where every pixel's red, green and blue are its luma and its alpha
    /// is unchanged. The input is not changed.
    /// </summary>
    /// <param name="rgba">Pixels as RGBA bytes, row by row (four bytes a pixel).</param>
    /// <exception cref="ArgumentException">The length is not a multiple of four.</exception>
    public static byte[] ToLuma(byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (rgba.Length % 4 != 0)
        {
            throw new ArgumentException("An RGBA buffer holds four bytes a pixel.", nameof(rgba));
        }

        var grey = new byte[rgba.Length];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            var luma = Luma(rgba[i], rgba[i + 1], rgba[i + 2]);
            grey[i] = luma;
            grey[i + 1] = luma;
            grey[i + 2] = luma;
            grey[i + 3] = rgba[i + 3];
        }

        return grey;
    }
}
