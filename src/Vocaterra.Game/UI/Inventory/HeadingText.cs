namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// What a doll column's heading says (I04b, I04c): the bare Spanish noun (<c>ropa</c>, no article, I04c) while it fits, else a short mark of its first letters (<c>apa</c>);
/// the noun with its article and the English are on hover whatever is drawn. Plain numbers and a measuring function, so a test checks it with a stand-in font.
/// </summary>
public static class HeadingText
{
    /// <summary>How many letters the short mark keeps.</summary>
    public const int MarkLetters = 3;

    /// <summary>The bare noun when it fits <paramref name="width"/>, else its short mark.</summary>
    /// <param name="noun">The noun alone, without its article.</param>
    /// <param name="width">The room in pixels.</param>
    /// <param name="measure">The width of a text in the panel's font.</param>
    public static string Pick(string noun, int width, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(noun);
        ArgumentNullException.ThrowIfNull(measure);
        return measure(noun) <= width ? noun : Mark(noun);
    }

    /// <summary>The short mark of a noun: its first letters.</summary>
    /// <param name="noun">The noun.</param>
    public static string Mark(string noun)
    {
        ArgumentNullException.ThrowIfNull(noun);
        return noun.Length <= MarkLetters ? noun : noun[..MarkLetters];
    }
}
