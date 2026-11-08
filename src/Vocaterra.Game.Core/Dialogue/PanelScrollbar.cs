namespace Vocaterra.Game.Core.Dialogue;

/// <summary>
/// The panel's scrollbar (U06): <see cref="ThumbTop"/> and <see cref="ThumbHeight"/> in pixels from the top of the track, which spans the lines' height.
/// </summary>
public readonly record struct PanelScrollbar(int ThumbTop, int ThumbHeight)
{
    /// <summary>The thinnest thumb, so a long page still leaves something to see.</summary>
    public const int MinThumb = 12;

    /// <summary>
    /// The bar for a fit, or <see langword="null"/> when every line is in view. The thumb's size is the rows in view against all rows (a line the collapsed panel
    /// hides counts as one row); its place follows how many rows lie above against below, so the newest lines put it at the bottom.
    /// </summary>
    /// <param name="blocks">The blocks, as <see cref="DialogueModel.PanelRows"/> gives them, wrapped.</param>
    /// <param name="fit">What <see cref="PanelFitter"/> chose.</param>
    /// <param name="hiddenBefore">The lines the collapsed panel leaves out before its blocks.</param>
    /// <param name="trackHeight">The lines' height in pixels.</param>
    public static PanelScrollbar? For(IReadOnlyList<PanelBlock> blocks, PanelFit fit, int hiddenBefore, int trackHeight)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(fit);
        if (trackHeight <= 0)
        {
            return null;
        }

        var above = hiddenBefore;
        var shown = 0;
        var below = 0;
        for (var i = 0; i < blocks.Count; i++)
        {
            var rows = blocks[i].Rows + (i < fit.ShowEnglish.Count && fit.ShowEnglish[i] ? blocks[i].EnglishRows : 0);
            if (i < fit.First)
            {
                above += blocks[i].Rows;
            }
            else if (i < fit.End)
            {
                shown += fit.Shows(i) ? rows : 0;
            }
            else
            {
                below += blocks[i].Rows;
            }
        }

        if (above == 0 && below == 0)
        {
            return null;
        }

        var total = above + shown + below;
        var height = Math.Clamp((int)Math.Round((double)trackHeight * shown / Math.Max(1, total)), Math.Min(MinThumb, trackHeight), trackHeight);
        var top = (int)Math.Round((trackHeight - height) * (double)above / (above + below));
        return new PanelScrollbar(Math.Clamp(top, 0, trackHeight - height), height);
    }
}
