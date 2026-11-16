using System.Globalization;

namespace Vocaterra.Game.Core.Items;

/// <summary>An opaque colour of an item's look (I03a): three bytes, read from a <c>#rgb</c> or <c>#rrggbb</c> text. The core holds no engine colour type; the front-end turns it into its own.</summary>
/// <param name="R">The red, 0 to 255.</param>
/// <param name="G">The green, 0 to 255.</param>
/// <param name="B">The blue, 0 to 255.</param>
public readonly record struct LookColour(byte R, byte G, byte B)
{
    /// <summary>Reads <c>#rgb</c> or <c>#rrggbb</c> (hexadecimal digits, either case).</summary>
    /// <param name="text">The colour's text.</param>
    /// <param name="colour">The colour when the text is one.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a colour.</returns>
    public static bool TryParse(string? text, out LookColour colour)
    {
        colour = default;
        if (text is not { Length: 4 or 7 } || text[0] != '#')
        {
            return false;
        }

        var digits = text.AsSpan(1);
        var width = digits.Length / 3;
        Span<byte> parts = stackalloc byte[3];
        for (var i = 0; i < 3; i++)
        {
            if (!byte.TryParse(digits.Slice(i * width, width), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            parts[i] = width == 1 ? (byte)(value * 17) : value;
        }

        colour = new LookColour(parts[0], parts[1], parts[2]);
        return true;
    }

    /// <summary>The colour as <c>#rrggbb</c> in lowercase.</summary>
    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";
}
