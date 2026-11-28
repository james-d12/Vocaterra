using System.Globalization;
using System.Text;

namespace Vocaterra.Game.Core.Worlds;

/// <summary>Where a seed came from.</summary>
public enum SeedSource
{
    /// <summary>The text was a number and is used as it is.</summary>
    Number,

    /// <summary>The text was other text and was hashed to a seed.</summary>
    Hashed,

    /// <summary>The text was blank and a random seed was drawn.</summary>
    Random,
}

/// <summary>A seed and where it came from.</summary>
/// <param name="Seed">The world seed.</param>
/// <param name="Source">Where it came from.</param>
public readonly record struct SeedChoice(ulong Seed, SeedSource Source);

/// <summary>
/// The seed a player typed (Q01): a number is the seed as it is, blank is a random seed, and any other text is hashed to one with a fixed hash (64-bit FNV-1a over the text's UTF-8 bytes, so the same
/// words make the same world on every machine and every run; <see cref="string.GetHashCode()"/> would not). The random draw is the front-end's: it passes the function, and this class reads no random source.
/// </summary>
public static class SeedText
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    /// <summary>Reads what was typed.</summary>
    /// <param name="text">The seed field's text.</param>
    /// <param name="random">Draws a random seed (asked only for blank text).</param>
    public static SeedChoice Parse(string? text, Func<ulong> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return new SeedChoice(random(), SeedSource.Random);
        }

        if (trimmed.All(char.IsAsciiDigit) && ulong.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return new SeedChoice(number, SeedSource.Number);
        }

        return new SeedChoice(Hash(trimmed), SeedSource.Hashed);
    }

    /// <summary>The seed text stands for: 64-bit FNV-1a of its UTF-8 bytes.</summary>
    /// <param name="text">The text.</param>
    public static ulong Hash(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hash = OffsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= Prime;
        }

        return hash;
    }
}
