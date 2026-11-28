namespace Vocaterra.Game.Core.Title;

/// <summary>A line of text being typed (the title's name and seed fields): the text, a caret, and the edits a text box takes. Plain state; the page draws it.</summary>
public sealed class TextField
{
    private string _text = string.Empty;

    /// <summary>Creates an empty field.</summary>
    /// <param name="maxLength">The most characters it takes.</param>
    public TextField(int maxLength)
    {
        MaxLength = maxLength;
    }

    /// <summary>The most characters the field takes.</summary>
    public int MaxLength { get; }

    /// <summary>The text.</summary>
    public string Text => _text;

    /// <summary>The caret's place: 0 before the first character, <see cref="string.Length"/> after the last.</summary>
    public int Caret { get; private set; }

    /// <summary>Replaces the text (cut to <see cref="MaxLength"/>), the caret after it.</summary>
    /// <param name="text">The text.</param>
    public void Set(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text.Length > MaxLength ? text[..MaxLength] : text;
        Caret = _text.Length;
    }

    /// <summary>Types a character at the caret; a control character, and any character beyond the length, is ignored.</summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it was taken.</returns>
    public bool Insert(char c)
    {
        if (char.IsControl(c) || _text.Length >= MaxLength)
        {
            return false;
        }

        _text = _text.Insert(Caret, c.ToString());
        Caret++;
        return true;
    }

    /// <summary>Deletes the character before the caret.</summary>
    public void Backspace()
    {
        if (Caret > 0)
        {
            _text = _text.Remove(Caret - 1, 1);
            Caret--;
        }
    }

    /// <summary>Deletes the character after the caret.</summary>
    public void Delete()
    {
        if (Caret < _text.Length)
        {
            _text = _text.Remove(Caret, 1);
        }
    }

    /// <summary>Moves the caret one character left.</summary>
    public void Left() => Caret = Math.Max(0, Caret - 1);

    /// <summary>Moves the caret one character right.</summary>
    public void Right() => Caret = Math.Min(_text.Length, Caret + 1);

    /// <summary>Moves the caret to the start.</summary>
    public void Home() => Caret = 0;

    /// <summary>Moves the caret to the end.</summary>
    public void End() => Caret = _text.Length;
}
