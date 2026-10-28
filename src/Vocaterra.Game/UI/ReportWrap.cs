namespace Vocaterra.Game.UI;

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
