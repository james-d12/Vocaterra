namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// What a doll column's heading says when the room is short (I04b): the Spanish noun with its article (<c>la ropa</c>) while it fits, else the noun alone, else a short mark of its first letters (<c>apa</c>);
/// the full words are on hover whatever is drawn. Plain numbers and a measuring function, so a test checks it with a stand-in font.
/// </summary>
public static class HeadingText
{
    /// <summary>How many letters the short mark keeps.</summary>
    public const int MarkLetters = 3;

    /// <summary>The longest of the three that fits <paramref name="width"/>, the mark when none does.</summary>
    /// <param name="withArticle">The noun with its article.</param>
    /// <param name="noun">The noun alone.</param>
    /// <param name="width">The room in pixels.</param>
    /// <param name="measure">The width of a text in the panel's font.</param>
    public static string Pick(string withArticle, string noun, int width, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(withArticle);
        ArgumentNullException.ThrowIfNull(noun);
        ArgumentNullException.ThrowIfNull(measure);
        if (measure(withArticle) <= width)
        {
            return withArticle;
        }

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
