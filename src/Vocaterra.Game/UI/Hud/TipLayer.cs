using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// The first-time tip of S16 (D-game-13), top left: a short English line the first time something happens (looking, walking, collecting, aiming at someone, talking),
/// with <c>Enter: next tip</c>, and a small <c>F1: controls</c> reminder. Tips come from <see cref="TipFeed"/> (game content); a scripted run draws none unless it asks
/// (<c>--tips</c>), so the demo screenshots stay uncluttered.
/// </summary>
public sealed class TipLayer : IFrameLayer
{
    /// <summary>The distance from the top and the left edge.</summary>
    public const int Margin = 12;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Back = new(10, 10, 20, 205);
    private static readonly Color Dim = UiPalette.TipDim.ToColor();

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.ShowTips || view.Check is not null || view.MicCheck is { IsActive: true })
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var ui = view.Session.Locale;
        var y = Margin;
        if (view.Session.Tips.Current is { } tip)
        {
            var width = Math.Min(480, view.ViewportWidth / 2);
            var rows = TextWrap.Wrap(tip.Text, width - 16, t => DrawHelpers.Width(font, t));
            var height = 8 + 20 + (rows.Count * 20) + 20;
            var box = new Rectangle(Margin, y, width, height);
            spriteBatch.Draw(view.Pixel, box, Back);
            DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, DialoguePanel.EnglishColor);
            DrawHelpers.Text(spriteBatch, font, ui.Ui("english") + " · tip", new Vector2(box.X + 8, box.Y + 6), Gold);
            for (var i = 0; i < rows.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, rows[i], new Vector2(box.X + 8, box.Y + 26 + (i * 20)), DialoguePanel.EnglishColor);
            }

            DrawHelpers.Text(spriteBatch, font, ui.Ui("tipNext"), new Vector2(box.X + 8, box.Bottom - 22), Dim);
            y = box.Bottom + 6;
        }

        DrawHelpers.Text(spriteBatch, font, ui.Ui("controlsHint"), new Vector2(Margin, y), Dim);
    }
}
