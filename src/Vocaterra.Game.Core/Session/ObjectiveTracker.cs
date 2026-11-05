using Vocaterra.Game.Core.Tip;

namespace Vocaterra.Game.Core.Session;

/// <summary>One objective's line in the tracker (U01): its pieces, and a shorter form for a canvas too narrow for them.</summary>
/// <param name="Pieces">The line's coloured pieces.</param>
/// <param name="Short">The same objective in a few words (a checklist: its title and how many steps are done); equal to <paramref name="Pieces"/> when it has no shorter form.</param>
public sealed record TrackerLine(IReadOnlyList<ObjectivePiece> Pieces, IReadOnlyList<ObjectivePiece> Short);

/// <summary>The objective tracker's content (U01): at most <see cref="MaxLines"/> lines and how many objectives did not fit (drawn as <c>+n</c>).</summary>
/// <param name="Lines">The lines, the most recently advanced objective first.</param>
/// <param name="More">How many more objectives there are than lines.</param>
public sealed record TrackerView(IReadOnlyList<TrackerLine> Lines, int More)
{
    /// <summary>The most lines the tracker shows.</summary>
    public const int MaxLines = 3;

    /// <summary>The text of the count of objectives left out (<c>+1</c>), or an empty text when none is.</summary>
    public string MoreText => More > 0 ? "+" + More.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
}
