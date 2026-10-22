using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

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
    private static readonly Color DoneEnglish = new(150, 190, 160);
    private static readonly Color Back = new(10, 10, 20, 190);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Objective is not { } objective)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var session = view.Session;
        var pieces = session is VoxelSession voxel
            ? voxel.ObjectiveHud() // the stage's line, or the checklist (S17)
            : session.English.ObjectivePieces(objective, lemma => session.Words.RecordOf(lemma).Known);
        var rows = Rows(pieces, font, (int)(view.ViewportWidth * WidthShare));
        var width = rows.Max(row => row.Sum(p => (int)DrawHelpers.Width(font, p.Text)));
        var height = 8 + (rows.Count * 20);
        var box = new Rectangle(view.ViewportWidth - width - (Margin * 2) - 8, Margin, width + 16, height);
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

    // The checklist on one row when it fits; otherwise the title on the first row and the steps on the second.
    private static List<List<ObjectivePiece>> Rows(IReadOnlyList<ObjectivePiece> pieces, FontStashSharp.SpriteFontBase font, int maxWidth)
    {
        var whole = pieces.Sum(p => (int)DrawHelpers.Width(font, p.Text));
        if (whole <= maxWidth)
        {
            return [pieces.ToList()];
        }

        var firstStep = pieces.ToList().FindIndex(static p => p.Kind == ObjectivePieceKind.Word);
        if (firstStep < 0)
        {
            return [pieces.ToList()]; // a stage line is one piece of English (S17): it is short, and it is never split
        }

        return [pieces.Take(firstStep).ToList(), pieces.Skip(firstStep).ToList()];
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
        var box = new Rectangle(12, view.ViewportHeight - 40, (int)DrawHelpers.Width(font, Text) + 16, 28);
        spriteBatch.Draw(view.Pixel, box, new Color(10, 10, 20, 170));
        DrawHelpers.Text(spriteBatch, font, Text, new Vector2(box.Left + 8, box.Top + 6), Dim);
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
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Empty = new(90, 90, 110);
    private static readonly Color Back = new(10, 10, 20, 215);

    /// <summary>
    /// Where the card is for a window: above the hotbar, and, while the dialogue panel is open (it covers the bottom of the window; S6 left the card
    /// hidden behind it), on top of the panel.
    /// </summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="dialogueOpen">Whether the dialogue panel is open.</param>
    /// <param name="supportTop">While the dialogue panel is open, the top of the support cards above it (<see cref="SupportLayout.Top"/>, S8) so the card stays clear of them; <see langword="null"/> when there are none.</param>
    public static Rectangle BoxFor(int viewportWidth, int viewportHeight, bool dialogueOpen, int? supportTop = null)
    {
        var box = new Rectangle((viewportWidth - Width) / 2, viewportHeight - Bottom - Height, Width, Height);
        if (dialogueOpen)
        {
            var panel = DialogueLayout.PanelBox(viewportWidth, viewportHeight);
            box.Y = Math.Max(4, Math.Min(panel.Y, supportTop ?? panel.Y) - 8 - Height);
        }

        return box;
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Cards.Current(view.Tick) is not { } shown)
        {
            return;
        }

        var age = view.Tick - shown.StartTick;
        var alpha = (float)Math.Clamp((WordCardQueue.Duration - age) / (double)FadeTicks, 0.0, 1.0);
        var supportTop = view.Dialogue.IsOpen ? SupportLayout.For(view.Session, view.ViewportWidth, view.ViewportHeight).Top : (int?)null;
        var box = BoxFor(view.ViewportWidth, view.ViewportHeight, view.Dialogue.IsOpen, supportTop);

        spriteBatch.Draw(view.Pixel, box, Back * alpha);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold * alpha);

        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(PhraseSize);
        DrawHelpers.Text(spriteBatch, small, "New word", new Vector2(box.Left + 14, box.Top + 8), Gold * alpha);
        DrawHelpers.Text(spriteBatch, large, shown.Card.Phrase, new Vector2(box.Left + 14, box.Top + 28), Ink * alpha);

        // The audio slot: an empty placeholder until S9 records the word.
        var slot = new Rectangle(box.Right - 86, box.Top + 26, 72, 40);
        spriteBatch.Draw(view.Pixel, slot, new Color(30, 30, 44) * alpha);
        DrawHelpers.Border(spriteBatch, view.Pixel, slot, 1, Empty * alpha);
        DrawHelpers.Text(spriteBatch, small, "audio", new Vector2(slot.Left + 12, slot.Top + 11), Empty * alpha);
    }
}
