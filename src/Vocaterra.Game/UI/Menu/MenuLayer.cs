using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Menu;

/// <summary>The menu (Esc): the main page, the settings and the controls, in English, over the dimmed world. The state is <see cref="MenuScreen"/>; this only draws it.</summary>
public sealed class MenuLayer : IFrameLayer
{
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Back = new(12, 16, 30, 240);
    private static readonly Color Dim = new(180, 180, 200);
    private static readonly Color Track = new(70, 76, 100);
    private static readonly Color Hover = new(36, 44, 74);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Menu is not { IsOpen: true } menu)
        {
            return;
        }

        var ui = view.Session.Locale;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        spriteBatch.Draw(view.Pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), new Color(0, 0, 0, 110));
        var layout = new MenuLayout(view.ViewportWidth, view.ViewportHeight, menu.Page);
        var box = Rect(layout.Panel);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, DialoguePanel.EnglishColor);
        var title = menu.Page switch { MenuPage.Settings => ui.Ui("menuSettings"), MenuPage.Controls => ui.Ui("menuControls"), _ => ui.Ui("menuTitle") };
        DrawHelpers.Text(spriteBatch, font, title, new Vector2(box.X + 16, box.Y + 12), Gold);
        switch (menu.Page)
        {
            case MenuPage.Main:
                DrawMain(spriteBatch, view, menu, layout, font);
                break;
            case MenuPage.Settings:
                DrawSettings(spriteBatch, view, menu, layout, font);
                break;
            default:
                DrawControls(spriteBatch, view, menu, layout, font);
                break;
        }
    }

    private static void DrawMain(SpriteBatch spriteBatch, FrameView view, MenuScreen menu, MenuLayout layout, FontStashSharp.SpriteFontBase font)
    {
        var ui = view.Session.Locale;
        string[] names = [ui.Ui("menuResume"), ui.Ui("menuSettings"), ui.Ui("menuControls"), ui.Ui("menuExit")];
        for (var i = 0; i < names.Length; i++)
        {
            var button = Rect(layout.Button(i));
            var selected = (int)menu.MainSelected == i;
            spriteBatch.Draw(view.Pixel, button, selected ? Hover : new Color(24, 30, 52));
            DrawHelpers.Border(spriteBatch, view.Pixel, button, selected ? 2 : 1, selected ? Gold : DialoguePanel.EnglishColor);
            var width = (int)DrawHelpers.Width(font, names[i]);
            DrawHelpers.Text(spriteBatch, font, names[i], new Vector2(button.X + ((button.Width - width) / 2), button.Y + ((button.Height - 18) / 2)), selected ? Gold : Color.White);
        }

        Hint(spriteBatch, view, layout, font, ui.Ui("menuHint"), withBack: false);
    }

    private static void DrawSettings(SpriteBatch spriteBatch, FrameView view, MenuScreen menu, MenuLayout layout, FontStashSharp.SpriteFontBase font)
    {
        var ui = view.Session.Locale;
        var rows = menu.Rows;
        for (var shown = 0; shown < layout.Capacity && menu.SettingsScroll + shown < rows.Count; shown++)
        {
            var index = menu.SettingsScroll + shown;
            var row = rows[index];
            var area = Rect(layout.Row(shown));
            var textY = area.Y + ((area.Height - 18) / 2);
            if (row.Kind == MenuRowKind.Heading)
            {
                DrawHelpers.Text(spriteBatch, font, ui.Ui(row.LabelKey), new Vector2(area.X + 8, textY), Gold);
                spriteBatch.Draw(view.Pixel, new Rectangle(area.X + 8, area.Bottom - 3, area.Width - 16, 1), new Color(255, 214, 64, 90));
                continue;
            }

            if (row.Kind == MenuRowKind.Subheading)
            {
                DrawHelpers.Text(spriteBatch, font, ui.Ui(row.LabelKey), new Vector2(area.X + 16, textY), Color.White);
                continue;
            }

            if (row.Kind == MenuRowKind.Note)
            {
                DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, ui.Ui(row.LabelKey), area.Width - 16), new Vector2(area.X + 8, textY), Dim);
                continue;
            }

            var selected = index == menu.SettingsSelected;
            if (selected || menu.Hover == index)
            {
                spriteBatch.Draw(view.Pixel, area, Hover);
            }

            if (selected)
            {
                DrawHelpers.Border(spriteBatch, view.Pixel, area, 1, Gold);
            }

            var color = selected ? Gold : DialoguePanel.EnglishColor;
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, ui.Ui(row.LabelKey), MenuLayout.LabelWidth(layout.Row(shown))), new Vector2(area.X + 8, textY), color);
            var controlX = MenuLayout.ControlX(layout.Row(shown));
            if (row.Kind == MenuRowKind.Slider)
            {
                var track = Rect(MenuLayout.Track(layout.Row(shown)));
                spriteBatch.Draw(view.Pixel, track, Track);
                var filled = (int)Math.Round(row.Fraction * track.Width);
                spriteBatch.Draw(view.Pixel, new Rectangle(track.X, track.Y, filled, track.Height), selected ? Gold : new Color(200, 160, 220));
                spriteBatch.Draw(view.Pixel, new Rectangle(track.X + Math.Clamp(filled - 3, 0, track.Width - 6), track.Y - 5, 6, track.Height + 10), selected ? Gold : Color.White);
                DrawHelpers.Text(spriteBatch, font, row.ValueText(ui.Ui), new Vector2(track.Right + 12, textY), color);
            }
            else if (row.Kind is MenuRowKind.Choice or MenuRowKind.Toggle)
            {
                var value = row.ValueText(ui.Ui);
                DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, "< " + value + " >", MenuLayout.ControlWidth(layout.Row(shown))), new Vector2(controlX, textY), color);
            }
            else
            {
                DrawHelpers.Text(spriteBatch, font, ui.KeyName(Core.Input.GameKey.Enter), new Vector2(controlX, textY), Dim);
            }
        }

        ScrollMarks(spriteBatch, view, layout, menu.SettingsScroll, rows.Count - layout.Capacity);
        Hint(spriteBatch, view, layout, font, ui.Ui("settingsHint"), withBack: true);
    }

    private static void DrawControls(SpriteBatch spriteBatch, FrameView view, MenuScreen menu, MenuLayout layout, FontStashSharp.SpriteFontBase font)
    {
        var ui = view.Session.Locale;
        var lines = view.ControlLines?.For(layout) ?? [];
        var panel = Rect(layout.Panel);
        var maxScroll = Math.Max(0, lines.Count - layout.LineCapacity);
        var first = Math.Clamp(menu.ControlsScroll, 0, maxScroll);
        var y = layout.ListTop;
        for (var i = first; i < lines.Count && i < first + layout.LineCapacity; i++)
        {
            var line = lines[i];
            if (line.IsHeading)
            {
                DrawHelpers.Text(spriteBatch, font, line.Text, new Vector2(panel.X + 16, y), Gold);
                spriteBatch.Draw(view.Pixel, new Rectangle(panel.X + 16, y + 18, panel.Width - 32, 1), new Color(255, 214, 64, 90));
            }
            else
            {
                if (line.Keys.Length > 0)
                {
                    DrawHelpers.Text(spriteBatch, font, line.Keys, new Vector2(panel.X + 16, y), Gold);
                }

                DrawHelpers.Text(spriteBatch, font, line.Text, new Vector2(panel.X + 16 + layout.KeysWidth, y), DialoguePanel.EnglishColor);
            }

            y += MenuLayout.LineHeight;
        }

        ScrollMarks(spriteBatch, view, layout, first, maxScroll);
        Hint(spriteBatch, view, layout, font, maxScroll > 0 ? ui.Ui("controlsClose") + "   " + ui.Ui("controlsScroll") : ui.Ui("controlsClose"), withBack: true);
    }

    // A thin bar at the right of the list when it scrolls: where the window of rows sits.
    private static void ScrollMarks(SpriteBatch spriteBatch, FrameView view, MenuLayout layout, int scroll, int maxScroll)
    {
        if (maxScroll <= 0)
        {
            return;
        }

        var bar = new Rectangle(layout.Panel.Right - 8, layout.ListTop, 3, layout.ListHeight);
        spriteBatch.Draw(view.Pixel, bar, Track);
        var knobHeight = Math.Max(16, bar.Height / 5);
        var knobY = bar.Y + (int)Math.Round((bar.Height - knobHeight) * (Math.Clamp(scroll, 0, maxScroll) / (double)maxScroll));
        spriteBatch.Draw(view.Pixel, new Rectangle(bar.X, knobY, bar.Width, knobHeight), Gold);
    }

    private static void Hint(SpriteBatch spriteBatch, FrameView view, MenuLayout layout, FontStashSharp.SpriteFontBase font, string text, bool withBack)
    {
        var panel = Rect(layout.Panel);
        var room = panel.Width - 32 - (withBack ? 116 : 0);
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, text, room), new Vector2(panel.X + 16, panel.Bottom - 28), Dim);
        if (!withBack)
        {
            return;
        }

        var back = Rect(layout.Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, back, 1, DialoguePanel.EnglishColor);
        var label = view.Session.Locale.Ui("menuBack");
        var width = (int)DrawHelpers.Width(font, label);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(back.X + ((back.Width - width) / 2), back.Y + 5), Color.White);
    }

    private static Rectangle Rect(PixelBox b) => new(b.X, b.Y, b.Width, b.Height);
}
