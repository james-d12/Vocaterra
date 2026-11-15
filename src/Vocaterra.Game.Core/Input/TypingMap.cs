using System.Text;

namespace Vocaterra.Game.Core.Input;

/// <summary>
/// One key of the typing map: with Alt held, <see cref="Key"/> (or <see cref="Shifted"/>, with Shift) types <see cref="Alt"/>.
/// </summary>
/// <param name="Key">The key, as the character it types without modifiers (a lower-case letter, or a mark's key such as <c>/</c>).</param>
/// <param name="Shifted">The character the same key types with Shift when that is not the capital (the question mark on the <c>/</c> key), or <see langword="null"/>.</param>
/// <param name="Alt">The character Alt gives.</param>
public sealed record TypingKey(char Key, char? Shifted, char Alt);

/// <summary>
/// The letters and marks for a keyboard that cannot type them, per language (<c>game/words.json</c>, <c>typing</c>): with Alt held, a key gives the
/// character the file gives it, and Shift gives the capital of a letter. The window's own text events still deliver the letters a layout or a
/// compose key makes. A German map gives <c>ä</c>, <c>ö</c>, <c>ü</c> and <c>ß</c>; the game has no Spanish of its own here.
/// </summary>
public sealed class TypingMap
{
    /// <summary>Creates the map from its keys; a key may be given once.</summary>
    /// <param name="keys">The keys in the order the hint lists them.</param>
    public TypingMap(IEnumerable<TypingKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Keys = keys.ToList();
        if (Keys.Select(static k => char.ToLowerInvariant(k.Key)).Distinct().Count() != Keys.Count)
        {
            throw new ArgumentException("A typing key is given twice.", nameof(keys));
        }
    }

    /// <summary>The keys, in the order the file gives them.</summary>
    public IReadOnlyList<TypingKey> Keys { get; }

    /// <summary>
    /// What a typed character becomes: with Alt held, the character Alt gives (<see cref="Map"/>), otherwise the character itself. The window's text
    /// event is the one place that sees the letters, so the dialogue, the report and the checks all go through here.
    /// </summary>
    /// <param name="typed">The character the platform typed.</param>
    /// <param name="alt">Whether Alt is held.</param>
    public char ForTyped(char typed, bool alt) => alt && Map(typed, char.IsUpper(typed)) is { } accent ? accent : typed;

    /// <summary>The character for Alt plus <paramref name="key"/>, or <see langword="null"/> when the map has no key for it.</summary>
    /// <param name="key">The key, as the character it types without modifiers or with Shift.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public char? Map(char key, bool shift)
    {
        foreach (var entry in Keys)
        {
            if (entry.Shifted is { } shifted && key == shifted)
            {
                return entry.Alt;
            }

            if (char.ToLowerInvariant(key) == char.ToLowerInvariant(entry.Key))
            {
                return shift && char.IsLetter(entry.Alt) ? char.ToUpperInvariant(entry.Alt) : entry.Alt;
            }
        }

        return null;
    }

    /// <summary>
    /// The hint that lists the map (<c>Alt+a e i o u: á é í ó ú   Alt+n: ñ   Alt+/: ¿</c>): the letter keys in a row share one entry, and each other key
    /// has its own. A German map shows its own letters with no code change.
    /// </summary>
    public string Hint()
    {
        var parts = new List<string>();
        var letters = new List<TypingKey>();
        foreach (var entry in Keys.Append(null))
        {
            if (entry is { } k && char.IsLetter(k.Key))
            {
                letters.Add(k);
                continue;
            }

            if (letters.Count > 0)
            {
                parts.Add(LettersPart(letters));
                letters.Clear();
            }

            if (entry is { } other)
            {
                parts.Add($"Alt+{other.Key}: {other.Alt}");
            }
        }

        return string.Join("   ", parts);
    }

    private static string LettersPart(IReadOnlyList<TypingKey> letters)
    {
        var keys = new StringBuilder("Alt+");
        var alts = new StringBuilder();
        for (var i = 0; i < letters.Count; i++)
        {
            keys.Append(i == 0 ? string.Empty : " ").Append(letters[i].Key);
            alts.Append(i == 0 ? string.Empty : " ").Append(letters[i].Alt);
        }

        return $"{keys}: {alts}";
    }
}
