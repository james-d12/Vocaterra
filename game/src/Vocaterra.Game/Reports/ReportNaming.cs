using System.Globalization;

namespace Vocaterra.Game.Reports;

/// <summary>
/// The file names of a report (S21, D-game-17): <c>report-s&lt;session&gt;-t&lt;tick&gt;-&lt;bug|feature&gt;</c>, with the Markdown file (<c>.md</c>) and the
/// screenshot (<c>.png</c>) sharing the stem. A stem already taken in the folder gets a counter (<c>-2</c>, <c>-3</c>...). The names come from the
/// session number, the tick and the kind only: never from the clock.
/// </summary>
public static class ReportNaming
{
    /// <summary>The most counters tried before the naming gives up.</summary>
    public const int MaxCounter = 999;

    /// <summary>The stem of a report: the session, the tick and the kind, with a counter from 2 on.</summary>
    /// <param name="session">The play session number.</param>
    /// <param name="tick">The simulation tick of the report.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="counter">1 for the plain name, then 2, 3... when the plain name is taken.</param>
    public static string Stem(int session, long tick, ReportKind kind, int counter = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(counter, 1);
        var plain = string.Create(CultureInfo.InvariantCulture, $"report-s{session}-t{tick}-{ReportKinds.Word(kind)}");
        return counter == 1 ? plain : string.Create(CultureInfo.InvariantCulture, $"{plain}-{counter}");
    }

    /// <summary>
    /// The first stem whose Markdown and screenshot names are both free. <paramref name="isTaken"/> is given a file name (for example
    /// <c>report-s1-t4681-bug.md</c>) and says whether the folder already holds it.
    /// </summary>
    /// <param name="session">The play session number.</param>
    /// <param name="tick">The simulation tick.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="isTaken">Whether a file of this name exists.</param>
    /// <exception cref="InvalidOperationException">Every counter up to <see cref="MaxCounter"/> is taken.</exception>
    public static string Choose(int session, long tick, ReportKind kind, Func<string, bool> isTaken)
    {
        ArgumentNullException.ThrowIfNull(isTaken);
        for (var counter = 1; counter <= MaxCounter; counter++)
        {
            var stem = Stem(session, tick, kind, counter);
            if (!isTaken(stem + ".md") && !isTaken(stem + ".png"))
            {
                return stem;
            }
        }

        throw new InvalidOperationException($"No free report name for session {session}, tick {tick} ({ReportKinds.Word(kind)}).");
    }
}
