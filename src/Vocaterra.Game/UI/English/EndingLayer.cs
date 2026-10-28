using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.English;

/// <summary>
/// The closing card of the session (S17, D-game-15): <c>Session complete</c>, the guide's closing line in Spanish (large) and in English (its own colour), for a few
/// seconds after the player's own fire is lit with the camp done. The English is drawn, the Spanish is spoken. The card is up for <see cref="SessionEnding.ShowTicks"/>
/// or until a key is pressed; the objective area keeps saying that the session is complete.
/// </summary>
public sealed class EndingLayer : IFrameLayer
{
    private const int LargeSize = 32;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Back = new(12, 16, 30, 236);
    private static readonly Color Dim = new(180, 180, 200);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Ending is not { } ending || !ending.Visible(view.Tick) || view.Check is not null)
        {
            return;
        }

        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(LargeSize);
        var ui = view.Session.English;
        var width = Math.Min(760, view.ViewportWidth - 48);
        var spanish = TextWrap.Wrap(ending.Spanish, width - 40, t => DrawHelpers.Width(large, t));
        var english = TextWrap.Wrap(DialoguePanel.EnglishPrefix + ending.English, width - 40, t => DrawHelpers.Width(small, t));
        var summary = ending.Summary.SelectMany(line => TextWrap.Wrap(line, width - 40, t => DrawHelpers.Width(small, t))).ToList();
        var layout = new EndingLayout(view.ViewportWidth, view.ViewportHeight, spanish.Count, english.Count, summary.Count);
        var box = new Rectangle(layout.Box.X, layout.Box.Y, layout.Box.Width, layout.Box.Height);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 3, Gold);
        var y = box.Y + 14;
        DrawHelpers.Text(spriteBatch, large, ui.Ui("sessionCompleteTitle"), new Vector2(box.X + 20, y), Gold);
        y += 44;
        foreach (var row in spanish)
        {
            DrawHelpers.Text(spriteBatch, large, row, new Vector2(box.X + 20, y), Ink);
            y += 40;
        }

        y += 4;
        foreach (var row in english)
        {
            DrawHelpers.Text(spriteBatch, small, row, new Vector2(box.X + 20, y), DialoguePanel.EnglishColor);
            y += 22;
        }

        if (layout.SummaryShown > 0)
        {
            // The session summary (X04, R01): what was done this session, in English, under the closing line. A short canvas gives up the last rows (X08), ending in an ellipsis.
            y += 14;
            DrawHelpers.Text(spriteBatch, small, ui.Ui("summaryTitle"), new Vector2(box.X + 20, y), Gold);
            y += 22;
            for (var i = 0; i < layout.SummaryShown; i++)
            {
                var row = layout.SummaryCut && i == layout.SummaryShown - 1 ? "…" : summary[i];
                DrawHelpers.Text(spriteBatch, small, row, new Vector2(box.X + 20, y), Ink);
                y += 20;
            }
        }

        DrawHelpers.Text(spriteBatch, small, ui.Ui("sessionCompleteKeys"), new Vector2(box.X + 20, box.Bottom - 26), Dim);
    }
}
