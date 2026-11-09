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
/// with <c>Enter: next tip</c>, and a small <c>Esc: menu</c> reminder. Tips come from <see cref="TipFeed"/> (game content); a scripted run draws none unless it asks
/// (<c>--tips</c>), so the demo screenshots stay uncluttered.
/// </summary>
public sealed class TipLayer : IFrameLayer
{
    /// <summary>The distance from the top and the left edge.</summary>
    public const int Margin = 12;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Back = new(10, 10, 20, 205);
    private static readonly Color Dim = UiPalette.TipDim.ToColor();

    /// <summary>
    /// The tip box, its rows, the controls hint's y and the block of both (U01), or <see langword="null"/> when no tip layer is drawn: at the top left, under the debug overlay
    /// when F3 shows it, and what the tracker keeps clear of (<see cref="HudLayout.TipBlock"/>).
    /// </summary>
    /// <param name="view">The frame's view.</param>
    /// <param name="font">The small font.</param>
    internal static (PixelBox? Box, IReadOnlyList<string> Rows, int HintY, PixelBox Block)? PlanFor(FrameView view, FontStashSharp.SpriteFontBase font)
    {
        if (!view.ShowTips || view.Check is not null || view.MicCheck is { IsActive: true })
        {
            return null;
        }

        var ui = view.Session.Locale;
        IReadOnlyList<string> rows = [];
        if (view.Session.Tips.Current is { } tip)
        {
            rows = TextWrap.Wrap(tip.Text, HudLayout.TipTextWidth(view.ViewportWidth), t => DrawHelpers.Width(font, t));
        }

        var placed = HudLayout.TipBlock(view.ViewportWidth, view.DebugOverlayBottom, view.Session.Tips.Current is null ? null : rows.Count, (int)Math.Ceiling(DrawHelpers.Width(font, ui.Ui("controlsHint"))));
        return (placed.Box, rows, placed.HintY, placed.Block);
    }

    /// <summary>The block of the tip box and the controls hint this frame, or <see langword="null"/> when none is drawn.</summary>
    /// <param name="view">The frame's view.</param>
    internal static PixelBox? BlockOf(FrameView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return PlanFor(view, view.Fonts.Get(FontProvider.NativeSize))?.Block;
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var font = view.Fonts.Get(FontProvider.NativeSize);
        if (PlanFor(view, font) is not { } plan)
        {
            return;
        }

        var ui = view.Session.Locale;
        if (plan.Box is { } placed)
        {
            var box = new Rectangle(placed.X, placed.Y, placed.Width, placed.Height);
            spriteBatch.Draw(view.Pixel, box, Back);
            DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, DialoguePanel.EnglishColor);
            DrawHelpers.Text(spriteBatch, font, ui.Ui("tipHeading", ("english", ui.Ui("english"))), new Vector2(box.X + 8, box.Y + 6), Gold);
            for (var i = 0; i < plan.Rows.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, plan.Rows[i], new Vector2(box.X + 8, box.Y + 26 + (i * 20)), DialoguePanel.EnglishColor);
            }

            DrawHelpers.Text(spriteBatch, font, ui.Ui("tipNext"), new Vector2(box.X + 8, box.Bottom - 22), Dim);
        }

        DrawHelpers.Text(spriteBatch, font, ui.Ui("controlsHint"), new Vector2(Margin, plan.HintY), Dim);
    }
}
