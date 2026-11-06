namespace Vocaterra.Game.Core.Dialogue;

/// <summary>
/// One block of the dialogue panel (U02b): a <see cref="PanelRow"/> with the rows its text takes once wrapped. <see cref="Index"/> and <see cref="Speaker"/>
/// say which line it is (-1 and <see cref="DialogueSpeaker.Coaching"/> for the coaching line); <see cref="Rows"/> is the Spanish text's rows and
/// <see cref="EnglishRows"/> the English under it (0 when none).
/// </summary>
public readonly record struct PanelBlock(int Index, DialogueSpeaker Speaker, int Rows, int EnglishRows);

/// <summary>
/// What the panel shows of its blocks (U02b): the blocks from <see cref="First"/> up to (not including) <see cref="End"/>, whether the row that says lines are
/// out of view above (<see cref="ShowEarlier"/>, counting <see cref="EarlierLines"/>) and below (<see cref="ShowNewer"/>, <see cref="NewerLines"/>) is
/// shown, and the English shown under each block (<see cref="ShowEnglish"/>, one per block).
/// </summary>
public sealed record PanelFit(int First, int End, bool ShowEarlier, int EarlierLines, bool ShowNewer, int NewerLines, IReadOnlyList<bool> ShowEnglish)
{
    /// <summary>Whether the block at <paramref name="block"/> is shown.</summary>
    public bool Shows(int block) => block >= First && block < End;
}

/// <summary>
/// Chooses the rows the dialogue panel shows in its room (U02b), as plain logic: the collapsed panel keeps the current exchange and drops, in order, the
/// English under the NPC's lines, then the hidden-lines row, and never the learner's line or the NPC's Spanish reply; the expanded panel shows a window of
/// the page that scrolls back from the newest lines, and says with a row how many lines are out of view above and below.
/// </summary>
public static class PanelFitter
{
    /// <summary>Fits the blocks into <paramref name="capacity"/> rows.</summary>
    /// <param name="blocks">The blocks, oldest first, as <see cref="DialogueModel.PanelRows"/> gives them (the coaching line last, if any).</param>
    /// <param name="capacity">The rows the panel has.</param>
    /// <param name="expanded">Whether the older lines show (the page); the collapsed panel shows the current exchange only.</param>
    /// <param name="scrollBack">In the expanded page: how many blocks the window is scrolled back from the newest.</param>
    /// <param name="hiddenBefore">The lines the collapsed panel leaves out before its blocks (the older lines of the page).</param>
    public static PanelFit Fit(IReadOnlyList<PanelBlock> blocks, int capacity, bool expanded, int scrollBack, int hiddenBefore)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var count = blocks.Count;
        var budget = Math.Max(0, capacity);
        var english = new bool[count];
        if (count == 0)
        {
            return new PanelFit(0, 0, hiddenBefore > 0, hiddenBefore, false, 0, english);
        }

        int EarlierCount(int first)
        {
            var lines = hiddenBefore;
            for (var i = 0; i < first; i++)
            {
                lines += blocks[i].Index >= 0 ? 1 : 0;
            }

            return lines;
        }

        if (expanded)
        {
            var end = count - Math.Clamp(scrollBack, 0, count - 1);
            var newerLines = 0;
            for (var i = end; i < count; i++)
            {
                newerLines += blocks[i].Index >= 0 ? 1 : 0;
            }

            // The window grows upward from the newest shown block while its rows, the English under them and the rows that say what is out of view fit.
            int Cost(int candidate)
            {
                var cost = (newerLines > 0 ? 1 : 0) + (EarlierCount(candidate) > 0 ? 1 : 0);
                for (var i = candidate; i < end; i++)
                {
                    cost += blocks[i].Rows + blocks[i].EnglishRows;
                }

                return cost;
            }

            var first = end - 1;
            for (var candidate = end - 2; candidate >= 0 && Cost(candidate) <= budget; candidate--)
            {
                first = candidate;
            }

            var earlier = EarlierCount(first);
            for (var i = 0; i < count; i++)
            {
                english[i] = blocks[i].EnglishRows > 0;
            }

            return new PanelFit(first, end, earlier > 0, earlier, newerLines > 0, newerLines, english);
        }

        // Collapsed: the exchange is the lines (the coaching line, if any, is the last block). The Spanish lines come first, the newest and the learner's line
        // (the oldest of the exchange) the last to go; then the coaching line; then the English under the lines; then the hidden-lines row.
        var coachingIndex = blocks[count - 1].Index < 0 ? count - 1 : count;
        var lineEnd = coachingIndex;
        var spokenUsed = 0;
        var firstLine = lineEnd;
        while (firstLine > 0 && spokenUsed + blocks[firstLine - 1].Rows <= budget)
        {
            firstLine--;
            spokenUsed += blocks[firstLine].Rows;
        }

        if (firstLine == lineEnd && lineEnd > 0)
        {
            firstLine = lineEnd - 1; // a line taller than the room is shown (the panel clips its top), never nothing
            spokenUsed = blocks[firstLine].Rows;
        }

        var shownEnd = lineEnd;
        if (coachingIndex < count && spokenUsed + blocks[coachingIndex].Rows <= budget)
        {
            shownEnd = count;
            spokenUsed += blocks[coachingIndex].Rows;
        }

        var rowsUsed = spokenUsed;
        for (var i = shownEnd - 1; i >= firstLine; i--)
        {
            if (blocks[i].EnglishRows > 0 && rowsUsed + blocks[i].EnglishRows <= budget)
            {
                english[i] = true;
                rowsUsed += blocks[i].EnglishRows;
            }
        }

        var earlierLines = EarlierCount(firstLine);
        var showEarlier = earlierLines > 0 && rowsUsed + 1 <= budget;
        return new PanelFit(firstLine, shownEnd, showEarlier, earlierLines, false, 0, english);
    }
}
