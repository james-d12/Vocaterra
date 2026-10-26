using FontStashSharp;

namespace Vocaterra.Game.UI;

/// <summary>
/// The text renderer: FontStashSharp over a TTF read at run time (no SpriteFont, so no character regions: any glyph the TTF has can be drawn,
/// including the Spanish accents). Pixel Operator is a 16 px bitmap-style font, so ask for 16 or multiples of 16 to keep it sharp.
/// </summary>
public sealed class FontProvider
{
    /// <summary>The font's native pixel height; use it, or a multiple, as the size.</summary>
    public const int NativeSize = 16;

    private readonly FontSystem _system = new();

    /// <summary>Loads the TTF at <paramref name="path"/>.</summary>
    public FontProvider(string path)
    {
        _system.AddFont(File.ReadAllBytes(path));
    }

    /// <summary>The font at a pixel size.</summary>
    public SpriteFontBase Get(int size = NativeSize) => _system.GetFont(size);
}
