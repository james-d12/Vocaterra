using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.English;

/// <summary>
/// The controls help of S16 (D-game-13), on F1: every key and button of the game in English, from the game's content (<c>game/english.json</c>), in a panel over the
/// world. F1 or Esc closes it.
/// </summary>
public sealed class ControlsLayer : IFrameLayer
{
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Back = new(12, 16, 30, 240);
    private static readonly Color Dim = new(180, 180, 200);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.ControlsOpen)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var ui = view.Session.English;
        var (layout, rows) = LayoutFor(view, font, view.ControlsScroll);
        var box = new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, layout.Panel.Height);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, DialoguePanel.EnglishColor);
        DrawHelpers.Text(spriteBatch, font, ui.Ui("controlsTitle"), new Vector2(box.X + 16, box.Y + 12), Gold);
        var keysWidth = KeysWidth(layout.Panel.Width);
        var y = box.Y + ControlsLayout.HeaderHeight;
        for (var index = layout.First; index < layout.First + layout.Count; index++)
        {
            var (keys, text) = (rows[index].Keys, rows[index].Text);
            for (var i = 0; i < keys.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, keys[i], new Vector2(box.X + 16, y + (i * 20)), Gold);
            }

            for (var i = 0; i < text.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, text[i], new Vector2(box.X + 16 + keysWidth, y + (i * 20)), DialoguePanel.EnglishColor);
            }

            y += RowHeight(rows[index]);
        }

        var close = layout.Scrolls ? ui.Ui("controlsClose") + "   " + ScrollHint : ui.Ui("controlsClose");
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, close, box.Width - 32), new Vector2(box.X + 16, box.Bottom - 26), Dim);
    }

    /// <summary>What the footer adds when the controls do not all fit the canvas.</summary>
    public const string ScrollHint = "Up and Down: scroll";

    /// <summary>The key the help adds for the text size (X08); the other controls are the game's content.</summary>
    public const string TextSizeKeys = "F9";

    /// <summary>The help line for the text size key.</summary>
    public const string TextSizeText = "Change the size of the text: 100, 125 or 150 percent. It is kept in the save.";

    private static int KeysWidth(int panelWidth) => Math.Min(300, panelWidth / 3);

    private static int RowHeight((IReadOnlyList<string> Keys, IReadOnlyList<string> Text) row) => (Math.Max(row.Text.Count, row.Keys.Count) * 20) + 6;

    private static (ControlsLayout Layout, List<(IReadOnlyList<string> Keys, IReadOnlyList<string> Text)> Rows) LayoutFor(FrameView view, FontStashSharp.SpriteFontBase font, int scroll)
    {
        var width = Math.Min(820, view.ViewportWidth - 32);
        var keysWidth = KeysWidth(width);
        var textWidth = width - keysWidth - 32;
        var controls = view.Session.English.Controls.Select(c => (Keys: c.Keys, Text: c.Text)).Append((Keys: TextSizeKeys, Text: TextSizeText)).ToList();
        var rows = controls
            .Select(c => (Keys: TextWrap.Wrap(c.Keys, keysWidth - 8, t => DrawHelpers.Width(font, t)), Text: TextWrap.Wrap(c.Text, textWidth, t => DrawHelpers.Width(font, t))))
            .ToList();
        return (new ControlsLayout(view.ViewportWidth, view.ViewportHeight, rows.Select(RowHeight).ToList(), scroll), rows);
    }

    /// <summary>How far the controls help can scroll on the frame's canvas (the game clamps the scroll with it).</summary>
    /// <param name="view">The frame's view.</param>
    public static int MaxScrollFor(FrameView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return LayoutFor(view, view.Fonts.Get(FontProvider.NativeSize), 0).Layout.MaxScroll;
    }
}
