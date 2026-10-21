using System.Text;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The text box of the dialogue panel as plain logic: a line of text with a caret, edited by characters and by the editing keys. It accepts
/// any letter, digit, punctuation mark or symbol (so <c>á é í ó ú ü ñ ¿ ¡</c> and their capitals), the space, and nothing else: control
/// characters, line breaks and lone surrogates are refused, and so is a character that would pass <see cref="MaxLength"/>. The window's text
/// events feed <see cref="Insert(char)"/>; the keys feed the other methods. Characters arrive precomposed (NFC), as the platform's text
/// input delivers them.
/// </summary>
public sealed class TextEntry
{
    /// <summary>The default longest line, in characters.</summary>
    public const int DefaultMaxLength = 80;

    private readonly StringBuilder _text = new();

    /// <summary>Creates an empty box.</summary>
    /// <param name="maxLength">The most characters the box holds.</param>
    public TextEntry(int maxLength = DefaultMaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);
        MaxLength = maxLength;
    }

    /// <summary>The most characters the box holds.</summary>
    public int MaxLength { get; }

    /// <summary>The text typed so far.</summary>
    public string Text => _text.ToString();

    /// <summary>Where the caret is: the number of characters before it (0 to the length of the text).</summary>
    public int Caret { get; private set; }

    /// <summary>Whether the box holds no visible character (it is empty or only spaces).</summary>
    public bool IsBlank => Text.AsSpan().Trim().IsEmpty;

    /// <summary>Whether the box would take this character.</summary>
    public static bool IsAccepted(char c)
    {
        if (c == ' ')
        {
            return true;
        }

        if (char.IsControl(c) || char.IsSurrogate(c) || char.IsWhiteSpace(c))
        {
            return false;
        }

        return char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c);
    }

    /// <summary>Inserts a character at the caret. Returns false (and changes nothing) if it is refused or the box is full.</summary>
    public bool Insert(char c)
    {
        if (!IsAccepted(c) || _text.Length >= MaxLength)
        {
            return false;
        }

        _text.Insert(Caret, c);
        Caret++;
        return true;
    }

    /// <summary>Inserts each accepted character of the text in turn (a scripted line); returns how many went in.</summary>
    public int Insert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var inserted = 0;
        foreach (var c in text)
        {
            if (Insert(c))
            {
                inserted++;
            }
        }

        return inserted;
    }

    /// <summary>Removes the character before the caret. Returns false when there is none.</summary>
    public bool Backspace()
    {
        if (Caret == 0)
        {
            return false;
        }

        _text.Remove(Caret - 1, 1);
        Caret--;
        return true;
    }

    /// <summary>Removes the character after the caret. Returns false when there is none.</summary>
    public bool Delete()
    {
        if (Caret >= _text.Length)
        {
            return false;
        }

        _text.Remove(Caret, 1);
        return true;
    }

    /// <summary>Moves the caret one character left, if it can.</summary>
    public void MoveLeft() => Caret = Math.Max(0, Caret - 1);

    /// <summary>Moves the caret one character right, if it can.</summary>
    public void MoveRight() => Caret = Math.Min(_text.Length, Caret + 1);

    /// <summary>Moves the caret to the start.</summary>
    public void Home() => Caret = 0;

    /// <summary>Moves the caret to the end.</summary>
    public void End() => Caret = _text.Length;

    /// <summary>Empties the box.</summary>
    public void Clear()
    {
        _text.Clear();
        Caret = 0;
    }

    /// <summary>Returns the trimmed text and empties the box (what Enter sends).</summary>
    public string Take()
    {
        var text = Text.Trim();
        Clear();
        return text;
    }
}

/// <summary>
/// The Spanish letters and marks for a keyboard that cannot type them: with Alt held, a vowel gives the same vowel with an acute accent,
/// <c>N</c> gives <c>ñ</c>, <c>/</c> (the question mark key) gives <c>¿</c> and <c>1</c> (the exclamation mark key) gives <c>¡</c>. Shift gives
/// the capital. The window's own text events still deliver the letters a layout or a compose key makes.
/// </summary>
public static class AccentShortcuts
{
    /// <summary>The character for Alt plus <paramref name="key"/> (a lower-case letter, <c>/</c> or <c>1</c>), or <see langword="null"/>.</summary>
    /// <param name="key">The key, as the character it types without modifiers.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public static char? Map(char key, bool shift)
    {
        char? lower = char.ToLowerInvariant(key) switch
        {
            'a' => 'á',
            'e' => 'é',
            'i' => 'í',
            'o' => 'ó',
            'u' => 'ú',
            'n' => 'ñ',
            _ => null,
        };
        if (lower is { } letter)
        {
            return shift ? char.ToUpperInvariant(letter) : letter;
        }

        return key switch
        {
            '/' or '?' => '¿',
            '1' or '!' => '¡',
            _ => null,
        };
    }
}
