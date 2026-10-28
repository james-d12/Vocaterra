using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.UI;

/// <summary>Where the speaker's bubble goes on the screen (S15): its box, and where its little pointer touches the box and points at the speaker.</summary>
/// <param name="Box">The bubble's box in pixels.</param>
/// <param name="TailX">The pointer's tip, x.</param>
/// <param name="TailY">The pointer's tip, y (below the box: the bubble hangs over the speaker's head).</param>
/// <param name="TailBaseX">The middle of the pointer's base on the box's bottom edge.</param>
public readonly record struct BubbleLayout(PixelBox Box, int TailX, int TailY, int TailBaseX);

/// <summary>
/// The speaker's bubble (S15, reference B): a small dark panel over the speaking figure's head with the name in a warm colour over the line, and a pointer to the
/// head. It is anchored to the speaker's head on the screen and kept inside the window; when the head is off the screen there is no bubble (the panel along the
/// bottom still has every line). Plain maths: a test places a bubble with no window.
/// </summary>
public static class DialogueBubble
{
    /// <summary>The widest a bubble is, in pixels.</summary>
    public const int MaxWidth = 340;

    /// <summary>The padding inside the bubble.</summary>
    public const int Padding = 10;

    /// <summary>The height of the pointer.</summary>
    public const int TailHeight = 12;

    /// <summary>The distance kept from the window's edge.</summary>
    public const int Margin = 8;

    /// <summary>The most rows of the line a bubble shows (the rest is cut with an ellipsis).</summary>
    public const int MaxRows = 3;

    /// <summary>
    /// Places a bubble of a size over a head on the screen, or returns <see langword="null"/> when the head is not on the screen. The bubble is centred over the head, moved
    /// to stay inside the window, and the pointer's tip stays on the head.
    /// </summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="headX">The head's x on the screen.</param>
    /// <param name="headY">The head's y on the screen (the top of the head).</param>
    /// <param name="width">The bubble's width.</param>
    /// <param name="height">The bubble's height.</param>
    /// <param name="top">The highest y the bubble may have (S22: a playtest keeps it below the facilitator's label); the margin by default.</param>
    public static BubbleLayout? Place(int viewportWidth, int viewportHeight, double headX, double headY, int width, int height, int top = Margin)
    {
        if (headX < 0 || headX > viewportWidth || headY < 0 || headY > viewportHeight || width + (2 * Margin) > viewportWidth)
        {
            return null;
        }

        var x = (int)Math.Round(headX - (width / 2.0));
        x = Math.Clamp(x, Margin, viewportWidth - Margin - width);
        var bottom = (int)Math.Round(headY) - TailHeight;
        var y = Math.Max(Math.Max(Margin, top), bottom - height);
        var tailBase = Math.Clamp((int)Math.Round(headX), x + 16, x + width - 16);
        return new BubbleLayout(new PixelBox(x, y, width, height), (int)Math.Round(headX), (int)Math.Round(headY), tailBase);
    }

    /// <summary>
    /// Places a bubble in the band between a top and a bottom limit (X08: the lesson strip above it, the dialogue panel under it), or returns <see langword="null"/> when the head is
    /// not on the screen or the band is too short for the bubble and its pointer: then there is no bubble, and the lesson strip and the panel's log carry the line. The bubble
    /// hangs over the head as <see cref="Place"/> says, moved down into the band or up out of it.
    /// </summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="headX">The head's x.</param>
    /// <param name="headY">The top of the head, y.</param>
    /// <param name="width">The bubble's width.</param>
    /// <param name="height">The bubble's height.</param>
    /// <param name="top">The highest y the bubble may have.</param>
    /// <param name="bottom">The lowest y the bubble's pointer may reach (the top of whatever stands below, less a gap).</param>
    public static BubbleLayout? PlaceBetween(int viewportWidth, int viewportHeight, double headX, double headY, int width, int height, int top, int bottom)
    {
        if (bottom - Math.Max(top, Margin) < height + TailHeight)
        {
            return null;
        }

        if (Place(viewportWidth, viewportHeight, headX, headY, width, height, top) is not { } placed)
        {
            return null;
        }

        var y = Math.Min(placed.Box.Y, bottom - TailHeight - height);
        return placed with { Box = placed.Box with { Y = y } };
    }

    /// <summary>The line the bubble shows: the speaker's newest line (what the NPC said last), or <see langword="null"/> when it has said nothing yet.</summary>
    public static string? LineOf(DialogueModel dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        for (var i = dialogue.Lines.Count - 1; i >= 0; i--)
        {
            if (dialogue.Lines[i].Speaker is DialogueSpeaker.Guide or DialogueSpeaker.Correction)
            {
                return dialogue.Lines[i].Text;
            }
        }

        return null;
    }

    /// <summary>The rows of a line for a text width, cut to <see cref="MaxRows"/> with an ellipsis on the last when it is longer.</summary>
    public static IReadOnlyList<string> Rows(string line, float textWidth, Func<string, float> measure)
    {
        var rows = TextWrap.Wrap(line, textWidth, measure);
        if (rows.Count <= MaxRows)
        {
            return rows;
        }

        var cut = rows.Take(MaxRows).ToList();
        cut[MaxRows - 1] = cut[MaxRows - 1].TrimEnd() + "…";
        return cut;
    }
}
