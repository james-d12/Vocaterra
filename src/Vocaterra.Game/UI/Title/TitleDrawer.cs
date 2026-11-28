using System.Globalization;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Title;
using Vocaterra.Game.Core.Worlds;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Menu;

namespace Vocaterra.Game.UI.Title;

/// <summary>
/// Draws the title screen (Q01): a night sky over two ranges of hills, the game's name, and the page in the Esc menu's look (<see cref="MenuPainter"/>'s panel, buttons, colours and text size). The state is
/// <see cref="TitleScreen"/>, the places are <see cref="TitleLayout"/>; the settings page is the menu's own page, drawn by <see cref="MenuPainter"/>.
/// </summary>
public static class TitleDrawer
{
    private static readonly Color Error = new(255, 128, 112);
    private static readonly Color Good = new(150, 230, 160);
    private static readonly Color FieldFill = new(8, 10, 22);

    /// <summary>Draws the title screen.</summary>
    /// <param name="spriteBatch">The batch the canvas is drawn with (begun under the interface scale).</param>
    /// <param name="pixel">The one-pixel white texture.</param>
    /// <param name="fonts">The fonts.</param>
    /// <param name="ui">The locale whose words the pages use.</param>
    /// <param name="title">The title screen.</param>
    /// <param name="width">The canvas's width.</param>
    /// <param name="height">The canvas's height.</param>
    /// <param name="caretOn">Whether a text field's caret is drawn this frame (it blinks).</param>
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, FontProvider fonts, LocaleText ui, TitleScreen title, int width, int height, bool caretOn)
    {
        var font = fonts.Get(FontProvider.NativeSize);
        Background(spriteBatch, pixel, width, height);
        if (title.Page == TitlePage.Settings)
        {
            MenuPainter.Draw(spriteBatch, pixel, font, ui, title.SettingsMenu, width, height, null, dimBackground: false);
            return;
        }

        var layout = new TitleLayout(width, height, title.Page);
        switch (title.Page)
        {
            case TitlePage.Main:
                DrawMain(spriteBatch, pixel, fonts, font, ui, title, layout, width);
                break;
            case TitlePage.New:
                DrawNew(spriteBatch, pixel, font, ui, title, layout, caretOn);
                break;
            case TitlePage.Load:
                DrawLoad(spriteBatch, pixel, font, ui, title, layout);
                break;
            default:
                DrawConfirm(spriteBatch, pixel, font, ui, title, layout);
                break;
        }
    }

    /// <summary>Draws the frame shown while the program makes the world the player chose (it takes some seconds): the night, the game's name and a line saying so.</summary>
    /// <param name="spriteBatch">The batch the canvas is drawn with (begun under the interface scale).</param>
    /// <param name="pixel">The one-pixel white texture.</param>
    /// <param name="fonts">The fonts.</param>
    /// <param name="ui">The locale whose words the line uses.</param>
    /// <param name="width">The canvas's width.</param>
    /// <param name="height">The canvas's height.</param>
    public static void DrawMaking(SpriteBatch spriteBatch, Texture2D pixel, FontProvider fonts, LocaleText ui, int width, int height)
    {
        Background(spriteBatch, pixel, width, height);
        var big = fonts.Get(FontProvider.NativeSize * 4);
        var name = ui.Ui("titleName");
        DrawHelpers.Text(spriteBatch, big, name, new Vector2((width - (int)DrawHelpers.Width(big, name)) / 2f, (height / 2f) - TitleLayout.NameHeight), MenuPainter.Gold);
        var font = fonts.Get(FontProvider.NativeSize);
        var line = ui.Ui("titleMaking");
        DrawHelpers.Text(spriteBatch, font, line, new Vector2((width - (int)DrawHelpers.Width(font, line)) / 2f, (height / 2f) + 16), MenuPainter.Dim);
    }

    // The night: a vertical gradient, a few stars, and two ranges of hills that are the same on every run (whole-number waves, no random numbers).
    private static void Background(SpriteBatch spriteBatch, Texture2D pixel, int width, int height)
    {
        const int bands = 32;
        for (var i = 0; i < bands; i++)
        {
            var top = i * height / bands;
            var bottom = (i + 1) * height / bands;
            var t = i / (float)(bands - 1);
            spriteBatch.Draw(pixel, new Rectangle(0, top, width, Math.Max(1, bottom - top)), Color.Lerp(new Color(20, 30, 66), new Color(6, 8, 20), t));
        }

        for (var i = 0; i < 48; i++)
        {
            var x = (int)(((uint)i * 2654435761u) % (uint)Math.Max(1, width));
            var y = (int)(((uint)i * 40503u * 7u) % (uint)Math.Max(1, height * 6 / 10));
            var size = i % 7 == 0 ? 2 : 1;
            spriteBatch.Draw(pixel, new Rectangle(x, y, size, size), new Color(220, 224, 255, i % 3 == 0 ? 220 : 120));
        }

        Hills(spriteBatch, pixel, width, height, baseHeight: height * 22 / 100, amplitude: 46, period: 360, phase: 90, new Color(13, 20, 40));
        Hills(spriteBatch, pixel, width, height, baseHeight: height * 11 / 100, amplitude: 34, period: 240, phase: 10, new Color(8, 12, 26));
    }

    private static void Hills(SpriteBatch spriteBatch, Texture2D pixel, int width, int height, int baseHeight, int amplitude, int period, int phase, Color colour)
    {
        const int step = 8;
        for (var x = 0; x < width; x += step)
        {
            var wave = Triangle(x + phase, period, amplitude) + Triangle((x * 3) + phase, period / 3, amplitude / 4);
            var top = height - baseHeight - wave;
            spriteBatch.Draw(pixel, new Rectangle(x, top, step, height - top), colour);
        }
    }

    // A triangle wave from 0 up to amplitude and back, over a period: the hills' outline.
    private static int Triangle(int x, int period, int amplitude)
    {
        var t = ((x % period) + period) % period;
        var half = period / 2;
        return (t < half ? t : period - t) * amplitude / Math.Max(1, half);
    }

    private static void DrawMain(SpriteBatch spriteBatch, Texture2D pixel, FontProvider fonts, SpriteFontBase font, LocaleText ui, TitleScreen title, TitleLayout layout, int width)
    {
        var big = fonts.Get(FontProvider.NativeSize * 4);
        var name = ui.Ui("titleName");
        var nameWidth = (int)DrawHelpers.Width(big, name);
        DrawHelpers.Text(spriteBatch, big, name, new Vector2((width - nameWidth) / 2f, layout.NameY), MenuPainter.Gold);
        MenuPainter.Panel(spriteBatch, pixel, layout.Panel);
        string[] names = [ui.Ui("titleNew"), ui.Ui("titleLoad"), ui.Ui("titleSettings"), ui.Ui("titleQuit")];
        for (var i = 0; i < names.Length; i++)
        {
            MenuPainter.DrawButton(spriteBatch, pixel, font, layout.Button(i), names[i], title.Selected == i, enabled: i != TitleSlot.LoadGame || title.HasWorlds);
        }

        Notice(spriteBatch, font, ui, title.Notice, layout);
        Hint(spriteBatch, font, layout, ui.Ui("titleHint"));
    }

    private static void DrawNew(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, LocaleText ui, TitleScreen title, TitleLayout layout, bool caretOn)
    {
        MenuPainter.Panel(spriteBatch, pixel, layout.Panel);
        Heading(spriteBatch, font, layout, ui.Ui("titleNewHeading"));
        var issue = title.NameIssue;
        Label(spriteBatch, font, layout.NameField, ui.Ui("titleNameLabel"));
        Field(spriteBatch, pixel, font, layout.NameField, title.Name, title.Selected == TitleSlot.NameField, title.Hover == TitleSlot.NameField, issue != NameProblem.None, caretOn);
        Label(spriteBatch, font, layout.SeedField, ui.Ui("titleSeedLabel"));
        Field(spriteBatch, pixel, font, layout.SeedField, title.Seed, title.Selected == TitleSlot.SeedField, title.Hover == TitleSlot.SeedField, false, caretOn);
        MenuPainter.DrawButton(spriteBatch, pixel, font, layout.RandomButton, ui.Ui("titleSeedRandom"), title.Selected == TitleSlot.RandomSeed || title.Hover == TitleSlot.RandomSeed);
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, ui.Ui("titleSeedBlank"), layout.NameField.Width), new Vector2(layout.SeedField.X, layout.SeedField.Bottom + 4), MenuPainter.Dim);

        var row = Rect(layout.TypeRow);
        var chosen = title.Selected == TitleSlot.WorldType || title.Hover == TitleSlot.WorldType;
        if (chosen)
        {
            spriteBatch.Draw(pixel, row, MenuPainter.Hover);
        }

        if (title.Selected == TitleSlot.WorldType)
        {
            DrawHelpers.Border(spriteBatch, pixel, row, 1, MenuPainter.Gold);
        }

        var colour = title.Selected == TitleSlot.WorldType ? MenuPainter.Gold : DialoguePanel.EnglishColor;
        var textY = row.Y + ((row.Height - 18) / 2);
        DrawHelpers.Text(spriteBatch, font, ui.Ui("titleTypeLabel"), new Vector2(row.X + 8, textY), colour);
        var type = ui.Ui(title.WorldType == WorldKind.Valley ? "titleTypeValley" : "titleTypeStreamed");
        var controlX = row.X + 8 + 150;
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, "< " + type + " >", row.Right - 8 - controlX), new Vector2(controlX, textY), colour);

        // Q03: the terrain options' rows go here, from layout.TerrainTop down (layout.TerrainHeight tall, 0 until then).
        var notice = title.Notice ?? (issue == NameProblem.None ? null : new TitleNotice(TitleText.KeyOf(issue), WorldNames.Clean(title.Name.Text)));
        Notice(spriteBatch, font, ui, notice, layout);
        MenuPainter.DrawButton(spriteBatch, pixel, font, layout.Create, ui.Ui("titleCreate"), title.Selected == TitleSlot.Create || title.Hover == TitleSlot.Create);
        MenuPainter.DrawButton(spriteBatch, pixel, font, layout.NewBack, ui.Ui("menuBack"), title.Selected == TitleSlot.NewBack || title.Hover == TitleSlot.Back);
        Hint(spriteBatch, font, layout, ui.Ui("titleNewHint"));
    }

    private static void DrawLoad(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, LocaleText ui, TitleScreen title, TitleLayout layout)
    {
        MenuPainter.Panel(spriteBatch, pixel, layout.Panel);
        Heading(spriteBatch, font, layout, ui.Ui("titleLoadHeading"));
        var worlds = title.Worlds;
        if (worlds.Count == 0)
        {
            DrawHelpers.Text(spriteBatch, font, ui.Ui("titleNoWorlds"), new Vector2(layout.Panel.X + 24, layout.ListTop + 12), MenuPainter.Dim);
        }

        for (var shown = 0; shown < layout.Capacity && title.LoadScroll + shown < worlds.Count; shown++)
        {
            var index = title.LoadScroll + shown;
            var world = worlds[index];
            var area = Rect(layout.WorldRow(shown));
            var selected = index == title.Selected;
            if (selected || title.Hover == index)
            {
                spriteBatch.Draw(pixel, area, MenuPainter.Hover);
            }

            if (selected)
            {
                DrawHelpers.Border(spriteBatch, pixel, area, 1, MenuPainter.Gold);
            }

            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, world.Name, area.Width - 16), new Vector2(area.X + 8, area.Y + 4), selected ? MenuPainter.Gold : Color.White);
            var second = world.Problem is { } problem ? problem : Describe(ui, world);
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, second, area.Width - 16), new Vector2(area.X + 8, area.Y + 25), world.Problem is null ? MenuPainter.Dim : Error);
        }

        if (worlds.Count > layout.Capacity)
        {
            var maxScroll = worlds.Count - layout.Capacity;
            var bar = new Rectangle(layout.Panel.Right - 8, layout.ListTop, 3, layout.Capacity * TitleLayout.WorldRowHeight);
            spriteBatch.Draw(pixel, bar, MenuPainter.Track);
            var knobHeight = Math.Max(16, bar.Height / 5);
            var knobY = bar.Y + (int)Math.Round((bar.Height - knobHeight) * (Math.Clamp(title.LoadScroll, 0, maxScroll) / (double)maxScroll));
            spriteBatch.Draw(pixel, new Rectangle(bar.X, knobY, bar.Width, knobHeight), MenuPainter.Gold);
        }

        var loadable = worlds.Count > 0 && worlds[Math.Clamp(title.Selected, 0, worlds.Count - 1)].Loadable;
        MenuPainter.DrawButton(spriteBatch, pixel, font, layout.LoadButton, ui.Ui("titleLoadButton"), title.Hover == TitleSlot.LoadAction, enabled: loadable);
        MenuPainter.DrawButton(spriteBatch, pixel, font, layout.DeleteButton, ui.Ui("titleDeleteButton"), title.Hover == TitleSlot.DeleteAction, enabled: worlds.Count > 0);
        MenuPainter.DrawButton(spriteBatch, pixel, font, layout.LoadBack, ui.Ui("menuBack"), title.Hover == TitleSlot.Back);
        Notice(spriteBatch, font, ui, title.Notice, layout);
        Hint(spriteBatch, font, layout, ui.Ui("titleLoadHint"));
    }

    private static void DrawConfirm(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, LocaleText ui, TitleScreen title, TitleLayout layout)
    {
        MenuPainter.Panel(spriteBatch, pixel, layout.Panel);
        Heading(spriteBatch, font, layout, ui.Ui("titleDeleteHeading"));
        var message = ui.Ui("titleConfirmDelete", ("name", title.PendingDelete?.Name ?? string.Empty));
        var y = layout.Message.Y;
        foreach (var line in TextWrap.Wrap(message, layout.Message.Width, text => DrawHelpers.Width(font, text)))
        {
            DrawHelpers.Text(spriteBatch, font, line, new Vector2(layout.Message.X, y), Color.White);
            y += 22;
        }

        MenuPainter.DrawButton(spriteBatch, pixel, font, layout.ConfirmDelete, ui.Ui("titleDeleteButton"), title.Selected == TitleSlot.ConfirmDelete || title.Hover == TitleSlot.ConfirmDelete);
        MenuPainter.DrawButton(spriteBatch, pixel, font, layout.ConfirmKeep, ui.Ui("titleKeepButton"), title.Selected == TitleSlot.ConfirmKeep || title.Hover == TitleSlot.ConfirmKeep);
        Hint(spriteBatch, font, layout, ui.Ui("titleConfirmHint"));
    }

    // The second line of a world's row: its seed, its type and when it was last played, and that it has no save yet when it has none.
    private static string Describe(LocaleText ui, WorldEntry world)
    {
        var meta = world.Meta!;
        var text = ui.Ui(
            "titleWorldLine",
            ("seed", meta.Seed.ToString(CultureInfo.InvariantCulture)),
            ("type", ui.Ui(meta.World == WorldKind.Valley ? "titleTypeValley" : "titleTypeStreamed")),
            ("played", PlayedAt(meta.LastPlayed)));
        return world.HasSave ? text : text + "   " + ui.Ui("titleNoSave");
    }

    private static string PlayedAt(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(unixSeconds, 0, 253_402_300_799)).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static void Heading(SpriteBatch spriteBatch, SpriteFontBase font, TitleLayout layout, string text) =>
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(layout.Panel.X + 16, layout.Panel.Y + 12), MenuPainter.Gold);

    private static void Label(SpriteBatch spriteBatch, SpriteFontBase font, PixelBox field, string text) =>
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(field.X, field.Y - 20), MenuPainter.Dim);

    private static void Field(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, PixelBox box, TextField field, bool focused, bool hover, bool problem, bool caretOn)
    {
        var area = Rect(box);
        spriteBatch.Draw(pixel, area, hover && !focused ? MenuPainter.Hover : FieldFill);
        DrawHelpers.Border(spriteBatch, pixel, area, focused ? 2 : 1, problem ? Error : focused ? MenuPainter.Gold : DialoguePanel.EnglishColor);
        var room = area.Width - 16;
        var shown = DrawHelpers.Fit(font, field.Text, room);
        DrawHelpers.Text(spriteBatch, font, shown, new Vector2(area.X + 8, area.Y + ((area.Height - 18) / 2)), Color.White);
        if (focused && caretOn && shown.Length == field.Text.Length)
        {
            var caretX = area.X + 8 + (int)DrawHelpers.Width(font, field.Text[..field.Caret]);
            spriteBatch.Draw(pixel, new Rectangle(caretX, area.Y + 6, 2, area.Height - 12), MenuPainter.Gold);
        }
    }

    // The line above the buttons: a refusal or a failure in red, a world deleted in green.
    private static void Notice(SpriteBatch spriteBatch, SpriteFontBase font, LocaleText ui, TitleNotice? notice, TitleLayout layout)
    {
        if (notice is null)
        {
            return;
        }

        var text = ui.Ui(notice.Key, ("name", notice.Name), ("why", notice.Detail));
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, text, layout.Notice.Width), new Vector2(layout.Notice.X, layout.Notice.Y + 2), notice.Key == "titleDeleted" ? Good : Error);
    }

    private static void Hint(SpriteBatch spriteBatch, SpriteFontBase font, TitleLayout layout, string text) =>
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, text, layout.Panel.Width - 32), new Vector2(layout.Panel.X + 16, layout.HintY), MenuPainter.Dim);

    private static Rectangle Rect(PixelBox b) => new(b.X, b.Y, b.Width, b.Height);
}
