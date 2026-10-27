using System.Text;
using Vocaterra.Game.Core.Input;

namespace Vocaterra.Game.UI;

/// <summary>
/// The multi-line text box of the report panel as plain logic (S21): the text with a caret, edited by characters and the editing keys. A new line
/// is a <c>\n</c>; every other character is accepted by <see cref="TextEntry.IsAccepted"/>. Up and Down move by line (the wrapping for the screen is
/// <see cref="ReportWrap"/>'s and needs no measure here).
/// </summary>
public sealed class ReportEntry
{
    /// <summary>The longest report text, in characters (new lines count).</summary>
    public const int DefaultMaxLength = 2000;

    private readonly StringBuilder _text = new();

    /// <summary>Creates an empty box.</summary>
    /// <param name="maxLength">The most characters the box holds.</param>
    public ReportEntry(int maxLength = DefaultMaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);
        MaxLength = maxLength;
    }

    /// <summary>The most characters the box holds.</summary>
    public int MaxLength { get; }

    /// <summary>The text typed so far.</summary>
    public string Text => _text.ToString();

    /// <summary>Where the caret is: the number of characters before it.</summary>
    public int Caret { get; private set; }

    /// <summary>Whether the box holds no visible character.</summary>
    public bool IsBlank => _text.ToString().AsSpan().Trim().IsEmpty;

    /// <summary>Inserts a character (or a new line, for <c>\n</c>) at the caret. Returns false when it is refused or the box is full.</summary>
    public bool Insert(char c)
    {
        if (_text.Length >= MaxLength || (c != '\n' && !TextEntry.IsAccepted(c)))
        {
            return false;
        }

        _text.Insert(Caret, c);
        Caret++;
        return true;
    }

    /// <summary>Inserts each character of the text in turn; returns how many went in.</summary>
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

    /// <summary>Removes the character before the caret.</summary>
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

    /// <summary>Removes the character after the caret.</summary>
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

    /// <summary>Moves the caret to the start of its line.</summary>
    public void Home() => Caret = StartOfLine(Caret);

    /// <summary>Moves the caret to the end of its line.</summary>
    public void End() => Caret = EndOfLine(Caret);

    /// <summary>Moves the caret up one line, to the same column (or the line's end when that line is shorter).</summary>
    public void MoveUp()
    {
        var lineStart = StartOfLine(Caret);
        if (lineStart == 0)
        {
            Caret = 0;
            return;
        }

        var column = Caret - lineStart;
        var previousEnd = lineStart - 1;
        var previousStart = StartOfLine(previousEnd);
        Caret = previousStart + Math.Min(column, previousEnd - previousStart);
    }

    /// <summary>Moves the caret down one line, to the same column (or the line's end when that line is shorter).</summary>
    public void MoveDown()
    {
        var lineEnd = EndOfLine(Caret);
        if (lineEnd >= _text.Length)
        {
            Caret = _text.Length;
            return;
        }

        var column = Caret - StartOfLine(Caret);
        var nextStart = lineEnd + 1;
        var nextEnd = EndOfLine(nextStart);
        Caret = nextStart + Math.Min(column, nextEnd - nextStart);
    }

    /// <summary>Empties the box.</summary>
    public void Clear()
    {
        _text.Clear();
        Caret = 0;
    }

    // The index of the first character of the line that holds position p (0 for the first line).
    private int StartOfLine(int p)
    {
        if (p <= 0)
        {
            return 0;
        }

        var before = _text.ToString(0, Math.Min(p, _text.Length));
        var newline = before.LastIndexOf('\n');
        return newline < 0 ? 0 : newline + 1;
    }

    // The index of the newline that ends the line holding position p, or the text's length on the last line.
    private int EndOfLine(int p)
    {
        var text = _text.ToString();
        if (p >= text.Length)
        {
            return text.Length;
        }

        var newline = text.IndexOf('\n', p);
        return newline < 0 ? text.Length : newline;
    }
}

/// <summary>
/// Breaks a text into the rows the box shows (S21): each logical line is split where it no longer fits, at a space when there is one. A row is a
/// start index and a length into the text (a new line is not part of a row), so the caret can be found in the rows.
/// </summary>
public static class ReportWrap
{
    /// <summary>The rows of the text for a width, measured with <paramref name="measure"/> (the font's width of a string).</summary>
    /// <param name="text">The text.</param>
    /// <param name="width">The width of a row, in the font's units.</param>
    /// <param name="measure">The width of a string.</param>
    public static List<(int Start, int Length)> Rows(string text, float width, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        var rows = new List<(int Start, int Length)>();
        var lineStart = 0;
        while (true)
        {
            var newline = text.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? text.Length : newline;
            if (lineEnd == lineStart)
            {
                rows.Add((lineStart, 0));
            }

            var start = lineStart;
            while (start < lineEnd)
            {
                var end = start + 1;
                while (end < lineEnd && measure(text[start..(end + 1)]) <= width)
                {
                    end++;
                }

                if (end < lineEnd)
                {
                    var space = text.LastIndexOf(' ', end - 1, end - start);
                    if (space > start)
                    {
                        end = space + 1;
                    }
                }

                rows.Add((start, end - start));
                start = end;
            }

            if (newline < 0)
            {
                return rows;
            }

            lineStart = newline + 1;
        }
    }

    /// <summary>The index in <paramref name="rows"/> of the row the caret is on.</summary>
    /// <param name="rows">The rows from <see cref="Rows"/>.</param>
    /// <param name="caret">The caret's index in the text.</param>
    public static int RowOf(IReadOnlyList<(int Start, int Length)> rows, int caret)
    {
        var found = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Start <= caret)
            {
                found = i;
            }
        }

        return found;
    }
}
