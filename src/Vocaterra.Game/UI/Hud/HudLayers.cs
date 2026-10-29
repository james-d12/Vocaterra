using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// The camp checklist, top right: <c>Campamento (camp): madera (wood) 0/5 · frutas (fruit) 0/3 · antorcha (torch) 0/1 · agua (water) 0/1</c> (S16, D-game-13).
/// A done step is drawn in green, an open one in white, the title in gold and the English in its own colour; the English of a step goes once its noun is known
/// (D-game-6) and the title's once every step's has. The text comes from <see cref="EnglishText.ObjectivePieces"/>, so the line is the one the content gives; a line
/// wider than the window's share wraps to a second row, the title first.
/// </summary>
public sealed class ObjectiveLayer : IFrameLayer
{
    /// <summary>The distance from the top and the right edge, in pixels.</summary>
    public const int Margin = 12;

    /// <summary>The share of the window's width the checklist may take before it wraps.</summary>
    public const double WidthShare = 0.62;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Open = new(245, 245, 245);
    private static readonly Color Done = new(120, 220, 130);
    private static readonly Color DoneEnglish = UiPalette.DoneEnglish.ToColor();
    private static readonly Color Back = new(10, 10, 20, 190);

    // The checklist's rows and box for a frame, or null when there is no checklist.
    private static (List<List<ObjectivePiece>> Rows, PixelBox Box)? PlanFor(FrameView view, FontStashSharp.SpriteFontBase font)
    {
        if (view.Objective is not { } objective)
        {
            return null;
        }

        var session = view.Session;
        var pieces = session is VoxelSession voxel
            ? voxel.ObjectiveHud() // the stage's line, or the checklist (S17)
            : session.English.ObjectivePieces(objective, lemma => session.Words.RecordOf(lemma).Known);
        var rows = HudLayout.ObjectiveRows(pieces, p => DrawHelpers.Width(font, p.Text), static p => p.Kind == ObjectivePieceKind.Word, (float)(view.ViewportWidth * WidthShare), view.ViewportWidth - (2 * Margin) - 16);
        var width = rows.Max(row => row.Sum(p => (int)DrawHelpers.Width(font, p.Text)));
        return (rows, HudLayout.ObjectiveBox(view.ViewportWidth, width, rows.Count));
    }

    /// <summary>The bottom of the checklist this frame, or 0 when it is not drawn: the workbench panel stands under it.</summary>
    /// <param name="view">The frame's view.</param>
    internal static int BottomOf(FrameView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (PlanFor(view, view.Fonts.Get(FontProvider.NativeSize)) is not { } plan)
        {
            return 0;
        }

        return view.Dialogue.IsOpen && !HudLayout.ObjectiveVisible(plan.Box, ConversationBoxes(view)) ? 0 : plan.Box.Bottom;
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var font = view.Fonts.Get(FontProvider.NativeSize);
        if (PlanFor(view, font) is not { } plan)
        {
            return;
        }

        var (rows, layoutBox) = plan;
        if (view.Dialogue.IsOpen && !HudLayout.ObjectiveVisible(layoutBox, ConversationBoxes(view)))
        {
            return; // it gives way to the lesson strip and the bubble on a short canvas (X08)
        }

        var box = new Rectangle(layoutBox.X, layoutBox.Y, layoutBox.Width, layoutBox.Height);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, Gold);
        var y = box.Top + 6;
        foreach (var row in rows)
        {
            var x = box.Left + 8;
            foreach (var piece in row)
            {
                DrawHelpers.Text(spriteBatch, font, piece.Text, new Vector2(x, y), ColourOf(piece));
                x += (int)DrawHelpers.Width(font, piece.Text);
            }

            y += 20;
        }
    }

    // What the conversation puts on the canvas this frame: the lesson strip and the bubble.
    private static List<PixelBox> ConversationBoxes(FrameView view)
    {
        var boxes = new List<PixelBox>();
        if (view.LessonStripBox is { } strip)
        {
            boxes.Add(strip);
        }

        if (ConversationFrame.For(view).Bubble is { } bubble)
        {
            boxes.Add(new PixelBox(bubble.Box.X, bubble.Box.Y, bubble.Box.Width, bubble.Box.Height + DialogueBubble.TailHeight));
        }

        return boxes;
    }

    private static Color ColourOf(ObjectivePiece piece) => piece.Kind switch
    {
        ObjectivePieceKind.Title => Gold,
        ObjectivePieceKind.English => piece.Done ? DoneEnglish : DialoguePanel.EnglishColor,
        ObjectivePieceKind.Word or ObjectivePieceKind.Count => piece.Done ? Done : Open,
        _ => Open,
    };
}

/// <summary>
/// The playtest's end key label (S12): <c>F10: end the run (facilitator)</c>, bottom left, on the world's frames only. It is for the facilitator
/// and says what the key does; the run ends with the post-check, the save and the thank-you screen.
/// </summary>
public sealed class PlaytestHintLayer : IFrameLayer
{
    /// <summary>The label the layer draws.</summary>
    public const string Text = PlaytestEnd.EndKeyName + ": end the run (facilitator)";

    private static readonly Color Dim = new(200, 200, 215);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Check is not null)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        // The place the conversation layout reserves (S22): top left, clear of the dialogue footer, the cards and the bubble.
        var label = ConversationLayout.FacilitatorLabel((int)DrawHelpers.Width(font, Text));
        var box = new Rectangle(label.X, label.Y, label.Width, label.Height);
        spriteBatch.Draw(view.Pixel, box, new Color(10, 10, 20, 170));
        DrawHelpers.Text(spriteBatch, font, Text, new Vector2(box.Left + 8, box.Top + 4), Dim);
    }
}

/// <summary>
/// The word card of a first pickup: the noun with its article (<c>la madera</c>) in a panel above the hotbar, a slot for its audio (the
/// audio comes with S9, so the slot is an empty placeholder for now) and the line <c>Palabra nueva</c>. It is shown for
/// <see cref="WordCardQueue.Duration"/> ticks; nothing in it asks the learner anything.
/// </summary>
public sealed class WordCardLayer : IFrameLayer
{
    private const int Width = 340;
    private const int Height = 84;
    private const int Bottom = 120;
    private const int FadeTicks = 30;
    private const int PhraseSize = 32;
    private const int ToastPhraseSize = 24;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Empty = new(90, 90, 110);
    private static readonly Color Back = new(10, 10, 20, 215);

    /// <summary>
    /// Where the card is for a window while no talk is open: above the hotbar. While a talk is open the card is a small toast placed by <see cref="ConversationLayout"/> (S22).
    /// </summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    public static Rectangle BoxFor(int viewportWidth, int viewportHeight) => new((viewportWidth - Width) / 2, viewportHeight - Bottom - Height, Width, Height);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Cards.Current(view.Tick) is not { } shown)
        {
            return;
        }

        var age = view.Tick - shown.StartTick;
        var alpha = (float)Math.Clamp((WordCardQueue.Duration - age) / (double)FadeTicks, 0.0, 1.0);
        var small = view.Fonts.Get(FontProvider.NativeSize);
        if (view.Dialogue.IsOpen)
        {
            // While a talk is open the card is a small toast in the conversation layout's column (S22): it never covers the speaker or the bubble.
            if (ConversationFrame.For(view).NewWord is { State: not CardPresence.Hidden } placement)
            {
                DrawToast(spriteBatch, view, small, shown.Card.Phrase, new Rectangle(placement.Box.X, placement.Box.Y, placement.Box.Width, placement.Box.Height), placement.State == CardPresence.Collapsed, alpha);
            }

            return;
        }

        var box = BoxFor(view.ViewportWidth, view.ViewportHeight);

        spriteBatch.Draw(view.Pixel, box, Back * alpha);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold * alpha);

        var large = view.Fonts.Get(PhraseSize);
        DrawHelpers.Text(spriteBatch, small, "New word", new Vector2(box.Left + 14, box.Top + 8), Gold * alpha);
        DrawHelpers.Text(spriteBatch, large, shown.Card.Phrase, new Vector2(box.Left + 14, box.Top + 28), Ink * alpha);

        // The audio slot: an empty placeholder until S9 records the word.
        var slot = new Rectangle(box.Right - 86, box.Top + 26, 72, 40);
        spriteBatch.Draw(view.Pixel, slot, new Color(30, 30, 44) * alpha);
        DrawHelpers.Border(spriteBatch, view.Pixel, slot, 1, Empty * alpha);
        DrawHelpers.Text(spriteBatch, small, "audio", new Vector2(slot.Left + 12, slot.Top + 11), UiPalette.Muted.ToColor() * alpha);
    }

    private static void DrawToast(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase small, string phrase, Rectangle box, bool collapsed, float alpha)
    {
        spriteBatch.Draw(view.Pixel, box, Back * alpha);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold * alpha);
        if (collapsed)
        {
            DrawHelpers.Text(spriteBatch, small, "New word: " + phrase, new Vector2(box.X + 8, box.Y + 4), Ink * alpha);
            return;
        }

        DrawHelpers.Text(spriteBatch, small, "New word", new Vector2(box.X + 12, box.Y + 6), Gold * alpha);
        DrawHelpers.Text(spriteBatch, view.Fonts.Get(ToastPhraseSize), phrase, new Vector2(box.X + 12, box.Y + 22), Ink * alpha);
    }
}
