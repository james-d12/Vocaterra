using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// Where the checklist (top right) and the toast (top centre) stand on a canvas (X08). The layers used to compute these inline, and a toast was as wide as its text
/// whatever the window. Plain numbers, so a test places them at every canvas the interface scale makes (1280 x 720 and 800 x 600 at 100, 125 and 150 percent).
/// </summary>
public static class HudLayout
{
    /// <summary>The distance of the checklist from the top and the right edge.</summary>
    public const int Margin = 12;

    /// <summary>The height of one row of text in the checklist or a toast.</summary>
    public const int RowHeight = 20;

    /// <summary>The y of the top of a toast: under the checklist, which is at most three rows tall.</summary>
    public const int ToastTop = 84;

    /// <summary>The padding either side of a toast's text.</summary>
    public const int ToastPadding = 14;

    /// <summary>The most rows the checklist takes.</summary>
    public const int MaxObjectiveRows = 3;

    /// <summary>
    /// The checklist's pieces in rows: on one row when they fit <paramref name="oneRowMax"/> (the share of the canvas the checklist may take); otherwise the title on the first
    /// row and the steps after it, and a steps row wider than <paramref name="maxWidth"/> (the canvas less its margins) goes on to a further row, at the start of a step (a step's word, its English and its count stay together). A piece is
    /// never cut, so a row is never wider than the canvas unless a single piece is.
    /// </summary>
    /// <typeparam name="T">The piece type.</typeparam>
    /// <param name="pieces">The pieces, in order.</param>
    /// <param name="width">The width of a piece.</param>
    /// <param name="isStep">Whether a piece belongs to the steps (the first one starts them).</param>
    /// <param name="oneRowMax">The width the whole line may take on one row.</param>
    /// <param name="maxWidth">The widest any row may be.</param>
    public static List<List<T>> ObjectiveRows<T>(IReadOnlyList<T> pieces, Func<T, float> width, Func<T, bool> isStep, float oneRowMax, float maxWidth)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(width);
        ArgumentNullException.ThrowIfNull(isStep);
        if (pieces.Sum(width) <= oneRowMax)
        {
            return [pieces.ToList()];
        }

        var firstStep = -1;
        for (var i = 0; i < pieces.Count; i++)
        {
            if (isStep(pieces[i]))
            {
                firstStep = i;
                break;
            }
        }

        if (firstStep < 0)
        {
            return [pieces.ToList()]; // a stage line is one piece of English (S17): it is short, and it is never split
        }

        var rows = new List<List<T>> { pieces.Take(firstStep).ToList() };
        var current = new List<T>();
        var used = 0f;
        for (var i = firstStep; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (isStep(piece))
            {
                // The step's own width: its word, and the English and the count that follow it up to the next step.
                var group = width(piece);
                for (var j = i + 1; j < pieces.Count && !isStep(pieces[j]); j++)
                {
                    group += width(pieces[j]);
                }

                if (current.Count > 0 && used + group > maxWidth && rows.Count < MaxObjectiveRows)
                {
                    rows.Add(current);
                    current = [];
                    used = 0;
                }
            }

            current.Add(piece);
            used += width(piece);
        }

        rows.Add(current);
        return rows;
    }

    /// <summary>
    /// The tracker's rows (U01): one row per objective line, a line too wide for a row in its shorter form, and the count of objectives left out (<c>+1</c>) after the last row's text. A
    /// tracker with one line and nothing left out keeps the checklist's wrapping (<see cref="ObjectiveRows{T}"/>: the camp's steps go on to further rows).
    /// </summary>
    /// <param name="tracker">The tracker's content.</param>
    /// <param name="width">The width of a piece.</param>
    /// <param name="oneRowMax">The width a single line may take on one row.</param>
    /// <param name="maxWidth">The widest any row may be.</param>
    public static List<List<ObjectivePiece>> TrackerRows(TrackerView tracker, Func<ObjectivePiece, float> width, float oneRowMax, float maxWidth)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(width);
        List<List<ObjectivePiece>> rows;
        if (tracker.Lines.Count == 1 && tracker.More == 0)
        {
            rows = ObjectiveRows(tracker.Lines[0].Pieces, width, static p => p.Kind == ObjectivePieceKind.Word, oneRowMax, maxWidth);
        }
        else
        {
            rows = tracker.Lines.Select(line => (line.Pieces.Sum(width) <= Math.Min(maxWidth, oneRowMax) ? line.Pieces : line.Short).ToList()).ToList();
        }

        if (tracker.More > 0 && rows.Count > 0)
        {
            rows[^1] = [.. rows[^1], new ObjectivePiece(" " + tracker.MoreText, ObjectivePieceKind.Plain)];
        }

        return rows;
    }

    /// <summary>The distance from the canvas's bottom to the top of the hotbar: the hotbar's slots are 48 tall with a 12 px margin.</summary>
    public const int HotbarTop = 60;

    /// <summary>
    /// The workbench panel's box (X08): at the right, centred on the canvas a little high as before, but moved down under the checklist (which can be three rows tall) and up off the
    /// hotbar, so it overlaps neither. The checklist wins when the canvas is too short for both (it is not: 533 x 400 holds 80 + 8 + 244 + 68).
    /// </summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    /// <param name="width">The panel's width.</param>
    /// <param name="height">The panel's height.</param>
    /// <param name="objectiveBottom">The bottom of the checklist, or 0 when it is not drawn.</param>
    public static PixelBox RecipePanelBox(int canvasWidth, int canvasHeight, int width, int height, int objectiveBottom)
    {
        var y = (canvasHeight / 2) - (height / 2) - 30;
        if (objectiveBottom > 0)
        {
            y = Math.Max(y, objectiveBottom + 8);
        }

        y = Math.Min(y, canvasHeight - HotbarTop - 8 - height);
        y = Math.Max(y, Margin);
        return new PixelBox(Math.Max(Margin, canvasWidth - width - 20), y, width, height);
    }

    /// <summary>The checklist's box.</summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="widest">The width of the widest row of text.</param>
    /// <param name="rows">The number of rows (up to <see cref="MaxObjectiveRows"/>).</param>
    public static PixelBox ObjectiveBox(int canvasWidth, int widest, int rows)
    {
        var width = Math.Min(widest + 16, canvasWidth - (2 * Margin));
        return new PixelBox(canvasWidth - Margin - width, Margin, width, 8 + (rows * RowHeight));
    }

    /// <summary>The tip box, its controls hint and both as one block (U01): plain numbers, so a test places them at every canvas.</summary>
    /// <param name="Box">The tip's own box, or <see langword="null"/> when no tip is up.</param>
    /// <param name="HintY">The y of the "Esc: menu" line under it.</param>
    /// <param name="Block">The tip box and the hint line together.</param>
    public readonly record struct TipPlacement(PixelBox? Box, int HintY, PixelBox Block);

    /// <summary>
    /// Where the tip box and the controls hint stand: at the top left, under the debug overlay when it is drawn (<paramref name="overlayBottom"/> above 0).
    /// </summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="overlayBottom">The bottom of the debug overlay, or 0 when it is not drawn.</param>
    /// <param name="tipRows">The wrapped rows of the tip's text, or <see langword="null"/> when no tip is up.</param>
    /// <param name="hintWidth">The width of the controls hint.</param>
    public static TipPlacement TipBlock(int canvasWidth, int overlayBottom, int? tipRows, int hintWidth)
    {
        const int left = 12;
        var y = overlayBottom > 0 ? Math.Max(Margin, overlayBottom + 6) : Margin;
        PixelBox? box = null;
        var width = hintWidth;
        var top = y;
        if (tipRows is { } rows)
        {
            width = Math.Min(480, canvasWidth / 2);
            box = new PixelBox(left, y, width, 8 + 20 + (rows * 20) + 20);
            y = box.Value.Bottom + 6;
        }

        return new TipPlacement(box, y, new PixelBox(left, top, width, y + 20 - top));
    }

    /// <summary>The width a tip's text may take: the tip box less its padding.</summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    public static int TipTextWidth(int canvasWidth) => Math.Min(480, canvasWidth / 2) - 16;

    /// <summary>
    /// The tracker's box clear of the tip block (U01): at the top right as <see cref="ObjectiveBox(int, int, int)"/> has it, and, when the tip box would touch it (a narrow canvas), under the tip block.
    /// </summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="widest">The width of the widest row of text.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="tip">The tip box with its controls hint, or <see langword="null"/> when none is drawn.</param>
    public static PixelBox ObjectiveBox(int canvasWidth, int widest, int rows, PixelBox? tip)
    {
        var box = ObjectiveBox(canvasWidth, widest, rows);
        if (tip is { } block && block.X < box.Right + Margin && box.X < block.Right + Margin && box.Y < block.Bottom + Margin)
        {
            box = box with { Y = block.Bottom + 6 };
        }

        return box;
    }

    /// <summary>
    /// Whether the checklist is drawn: it gives way (is left out) when it would overlap what the conversation puts on the canvas, for example the lesson strip on a short
    /// canvas. The conversation is what the learner is looking at then, and the lesson strip carries the step.
    /// </summary>
    /// <param name="box">The checklist's box.</param>
    /// <param name="avoid">The boxes of the conversation (lesson strip, bubble, cards).</param>
    public static bool ObjectiveVisible(PixelBox box, IEnumerable<PixelBox> avoid)
    {
        ArgumentNullException.ThrowIfNull(avoid);
        return !avoid.Any(other => ConversationLayout.Overlaps(box, other));
    }

    /// <summary>
    /// A toast's rows and box: the text wraps to the canvas (a long line goes onto a second row instead of running off the screen), the box is centred and stands at
    /// <see cref="ToastTop"/>.
    /// </summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="text">The toast's line.</param>
    /// <param name="measure">The width of a text in the small font.</param>
    public static (PixelBox Box, IReadOnlyList<string> Rows) Toast(int canvasWidth, string text, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        var room = canvasWidth - (2 * Margin) - (2 * ToastPadding);
        var rows = TextWrap.Wrap(text, room, measure);
        var widest = rows.Count == 0 ? 0 : (int)Math.Ceiling(rows.Max(measure));
        var width = Math.Min(widest + (2 * ToastPadding), canvasWidth - (2 * Margin));
        var height = 10 + (Math.Max(1, rows.Count) * RowHeight);
        return (new PixelBox((canvasWidth - width) / 2, ToastTop, width, height), rows);
    }
}
