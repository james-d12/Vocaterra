using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The camp checklist, top right: <c>Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1</c>. A done step is drawn in green, an open
/// one in white; the title in gold. The text is drawn from <see cref="Objective.Steps"/>, so the line is the one the objective reports.
/// </summary>
public sealed class ObjectiveLayer : IFrameLayer
{
    /// <summary>The distance from the top and the right edge, in pixels.</summary>
    public const int Margin = 12;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Open = new(245, 245, 245);
    private static readonly Color Done = new(120, 220, 130);
    private static readonly Color Back = new(10, 10, 20, 190);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Objective is not { } objective)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        const string Separator = " · ";
        var pieces = new List<(string Text, Color Colour)> { (Objective.Title + ": ", Gold) };
        for (var i = 0; i < objective.Steps.Count; i++)
        {
            var step = objective.Steps[i];
            if (i > 0)
            {
                pieces.Add((Separator, Open));
            }

            pieces.Add((step.Text, step.Done ? Done : Open));
        }

        var width = pieces.Sum(p => (int)DrawHelpers.Width(font, p.Text));
        var box = new Rectangle(view.ViewportWidth - width - (Margin * 2) - 8, Margin, width + 16, 28);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, Gold);
        var x = box.Left + 8;
        foreach (var (text, colour) in pieces)
        {
            DrawHelpers.Text(spriteBatch, font, text, new Vector2(x, box.Top + 6), colour);
            x += (int)DrawHelpers.Width(font, text);
        }
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

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Cards.Current(view.Tick) is not { } shown)
        {
            return;
        }

        var age = view.Tick - shown.StartTick;
        var alpha = (float)Math.Clamp((WordCardQueue.Duration - age) / (double)FadeTicks, 0.0, 1.0);
        var viewport = new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight);
        var box = new Rectangle((viewport.Width - Width) / 2, viewport.Height - Bottom - Height, Width, Height);
        spriteBatch.Draw(view.Pixel, box, Back * alpha);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold * alpha);

        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(PhraseSize);
        DrawHelpers.Text(spriteBatch, small, "Palabra nueva", new Vector2(box.Left + 14, box.Top + 8), Gold * alpha);
        DrawHelpers.Text(spriteBatch, large, shown.Card.Phrase, new Vector2(box.Left + 14, box.Top + 28), Ink * alpha);

        // The audio slot: an empty placeholder until S9 records the word.
        var slot = new Rectangle(box.Right - 86, box.Top + 26, 72, 40);
        spriteBatch.Draw(view.Pixel, slot, new Color(30, 30, 44) * alpha);
        DrawHelpers.Border(spriteBatch, view.Pixel, slot, 1, Empty * alpha);
        DrawHelpers.Text(spriteBatch, small, "audio", new Vector2(slot.Left + 12, slot.Top + 11), Empty * alpha);
    }
}
