using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Menu;

/// <summary>
/// Draws the menu's pages from a <see cref="MenuScreen"/> (the main page, the settings, the controls), in English, over whatever is behind: the Esc menu over the dimmed world (<see cref="MenuLayer"/>) and
/// the title screen's settings page over its own background (Q01). The state is the screen's; this only draws it. The colours are the menu's, which the title's own pages share.
/// </summary>
public static class MenuPainter
{
    /// <summary>The gold of a title, a chosen row and a border of the chosen thing.</summary>
    public static readonly Color Gold = new(255, 214, 64);

    /// <summary>The panel's fill.</summary>
    public static readonly Color Back = new(12, 16, 30, 240);

    /// <summary>The colour of a hint and of a secondary line.</summary>
    public static readonly Color Dim = new(180, 180, 200);

    /// <summary>The track of a slider and of a scroll bar.</summary>
    public static readonly Color Track = new(70, 76, 100);

    /// <summary>The fill of the row or button the pointer is on or the cursor has chosen.</summary>
    public static readonly Color Hover = new(36, 44, 74);

    /// <summary>The fill of a button that is not chosen.</summary>
    public static readonly Color Button = new(24, 30, 52);

    /// <summary>Draws the open page of <paramref name="menu"/>.</summary>
    /// <param name="spriteBatch">The batch the canvas is drawn with (begun under the interface scale).</param>
    /// <param name="pixel">The one-pixel white texture.</param>
    /// <param name="font">The font at its native size.</param>
    /// <param name="ui">The locale whose words the page uses.</param>
    /// <param name="menu">The menu.</param>
    /// <param name="width">The canvas's width.</param>
    /// <param name="height">The canvas's height.</param>
    /// <param name="controlLines">The controls page's wrapped lines, or <see langword="null"/> when there is no controls page.</param>
    /// <param name="dimBackground">Whether to dim what is behind (the Esc menu over the world); the title draws its own background.</param>
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, LocaleText ui, MenuScreen menu, int width, int height, ControlsPageCache? controlLines, bool dimBackground)
    {
        if (dimBackground)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, width, height), new Color(0, 0, 0, 110));
        }

        var layout = new MenuLayout(width, height, menu.Page);
        var box = Rect(layout.Panel);
        spriteBatch.Draw(pixel, box, Back);
        DrawHelpers.Border(spriteBatch, pixel, box, 2, DialoguePanel.EnglishColor);
        var title = menu.Page switch { MenuPage.Settings => ui.Ui("menuSettings"), MenuPage.Controls => ui.Ui("menuControls"), _ => ui.Ui("menuTitle") };
        DrawHelpers.Text(spriteBatch, font, title, new Vector2(box.X + 16, box.Y + 12), Gold);
        switch (menu.Page)
        {
            case MenuPage.Main:
                DrawMain(spriteBatch, pixel, font, ui, menu, layout);
                break;
            case MenuPage.Settings:
                DrawSettings(spriteBatch, pixel, font, ui, menu, layout);
                break;
            default:
                DrawControls(spriteBatch, pixel, font, ui, menu, layout, controlLines);
                break;
        }
    }

    /// <summary>The panel of a page, filled and bordered, as every menu page has it.</summary>
    /// <param name="spriteBatch">The batch.</param>
    /// <param name="pixel">The one-pixel white texture.</param>
    /// <param name="box">The panel.</param>
    public static void Panel(SpriteBatch spriteBatch, Texture2D pixel, PixelBox box)
    {
        var area = Rect(box);
        spriteBatch.Draw(pixel, area, Back);
        DrawHelpers.Border(spriteBatch, pixel, area, 2, DialoguePanel.EnglishColor);
    }

    /// <summary>A button of the menu's look: chosen (gold border, gold text) or not, and greyed when it cannot be used.</summary>
    /// <param name="spriteBatch">The batch.</param>
    /// <param name="pixel">The one-pixel white texture.</param>
    /// <param name="font">The font.</param>
    /// <param name="box">The button.</param>
    /// <param name="text">Its label.</param>
    /// <param name="selected">Whether the cursor or the pointer is on it.</param>
    /// <param name="enabled">Whether it can be used (a greyed one is drawn dim and never gold).</param>
    public static void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, PixelBox box, string text, bool selected, bool enabled = true)
    {
        var button = Rect(box);
        var chosen = selected && enabled;
        spriteBatch.Draw(pixel, button, chosen ? Hover : Button);
        DrawHelpers.Border(spriteBatch, pixel, button, chosen ? 2 : 1, chosen ? Gold : enabled ? DialoguePanel.EnglishColor : new Color(70, 76, 100));
        var width = (int)DrawHelpers.Width(font, text);
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(button.X + ((button.Width - width) / 2), button.Y + ((button.Height - 18) / 2)), chosen ? Gold : enabled ? Color.White : new Color(110, 116, 140));
    }

    private static void DrawMain(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, LocaleText ui, MenuScreen menu, MenuLayout layout)
    {
        string[] names = [ui.Ui("menuResume"), ui.Ui("menuSettings"), ui.Ui("menuControls"), ui.Ui("menuMainMenu"), ui.Ui("menuExit")];
        for (var i = 0; i < names.Length; i++)
        {
            DrawButton(spriteBatch, pixel, font, layout.Button(i), names[i], (int)menu.MainSelected == i);
        }

        Hint(spriteBatch, pixel, font, ui, layout, ui.Ui("menuHint"), withBack: false);
    }

    private static void DrawSettings(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, LocaleText ui, MenuScreen menu, MenuLayout layout)
    {
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
                spriteBatch.Draw(pixel, new Rectangle(area.X + 8, area.Bottom - 3, area.Width - 16, 1), new Color(255, 214, 64, 90));
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
                spriteBatch.Draw(pixel, area, Hover);
            }

            if (selected)
            {
                DrawHelpers.Border(spriteBatch, pixel, area, 1, Gold);
            }

            var color = selected ? Gold : DialoguePanel.EnglishColor;
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, ui.Ui(row.LabelKey), MenuLayout.LabelWidth(layout.Row(shown))), new Vector2(area.X + 8, textY), color);
            var controlX = MenuLayout.ControlX(layout.Row(shown));
            if (row.Kind == MenuRowKind.Slider)
            {
                var track = Rect(MenuLayout.Track(layout.Row(shown)));
                spriteBatch.Draw(pixel, track, Track);
                var filled = (int)Math.Round(row.Fraction * track.Width);
                spriteBatch.Draw(pixel, new Rectangle(track.X, track.Y, filled, track.Height), selected ? Gold : new Color(200, 160, 220));
                spriteBatch.Draw(pixel, new Rectangle(track.X + Math.Clamp(filled - 3, 0, track.Width - 6), track.Y - 5, 6, track.Height + 10), selected ? Gold : Color.White);
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

        ScrollMarks(spriteBatch, pixel, layout, menu.SettingsScroll, rows.Count - layout.Capacity);
        Hint(spriteBatch, pixel, font, ui, layout, ui.Ui("settingsHint"), withBack: true);
    }

    private static void DrawControls(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, LocaleText ui, MenuScreen menu, MenuLayout layout, ControlsPageCache? controlLines)
    {
        var lines = controlLines?.For(layout) ?? [];
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
                spriteBatch.Draw(pixel, new Rectangle(panel.X + 16, y + 18, panel.Width - 32, 1), new Color(255, 214, 64, 90));
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

        ScrollMarks(spriteBatch, pixel, layout, first, maxScroll);
        Hint(spriteBatch, pixel, font, ui, layout, maxScroll > 0 ? ui.Ui("controlsClose") + "   " + ui.Ui("controlsScroll") : ui.Ui("controlsClose"), withBack: true);
    }

    // A thin bar at the right of the list when it scrolls: where the window of rows sits.
    private static void ScrollMarks(SpriteBatch spriteBatch, Texture2D pixel, MenuLayout layout, int scroll, int maxScroll)
    {
        if (maxScroll <= 0)
        {
            return;
        }

        var bar = new Rectangle(layout.Panel.Right - 8, layout.ListTop, 3, layout.ListHeight);
        spriteBatch.Draw(pixel, bar, Track);
        var knobHeight = Math.Max(16, bar.Height / 5);
        var knobY = bar.Y + (int)Math.Round((bar.Height - knobHeight) * (Math.Clamp(scroll, 0, maxScroll) / (double)maxScroll));
        spriteBatch.Draw(pixel, new Rectangle(bar.X, knobY, bar.Width, knobHeight), Gold);
    }

    private static void Hint(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, LocaleText ui, MenuLayout layout, string text, bool withBack)
    {
        var panel = Rect(layout.Panel);
        var room = panel.Width - 32 - (withBack ? 116 : 0);
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, text, room), new Vector2(panel.X + 16, panel.Bottom - 28), Dim);
        if (!withBack)
        {
            return;
        }

        var back = Rect(layout.Back);
        DrawHelpers.Border(spriteBatch, pixel, back, 1, DialoguePanel.EnglishColor);
        var label = ui.Ui("menuBack");
        var width = (int)DrawHelpers.Width(font, label);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(back.X + ((back.Width - width) / 2), back.Y + 5), Color.White);
    }

    private static Rectangle Rect(PixelBox b) => new(b.X, b.Y, b.Width, b.Height);
}
