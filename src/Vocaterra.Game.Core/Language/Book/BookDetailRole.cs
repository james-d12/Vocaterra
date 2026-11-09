namespace Vocaterra.Game.Core.Language.Book;

/// <summary>What a <see cref="BookLineKind.Detail"/> line is on the right page (U07).</summary>
public enum BookDetailRole
{
    /// <summary>Running text, wrapped to the page, or a row (a lead, text, aside and a speaker button) when the line has a lead or a button.</summary>
    Text,

    /// <summary>The item's title when it is not the entry's own text (a lesson's full title, which wraps to two lines).</summary>
    Title,

    /// <summary>The line under the title: pips or a mark with a word or two.</summary>
    Sub,

    /// <summary>A section label with a rule under it and, optionally, a count at its right (<see cref="BookLine.Right"/>).</summary>
    Label,

    /// <summary>A strip of big numbers over their captions (<see cref="BookLine.Stats"/>).</summary>
    Stats,

    /// <summary>A strip of marks, one per unit of a lesson (<see cref="BookLine.Marks"/>), each with its number.</summary>
    Units,
}
