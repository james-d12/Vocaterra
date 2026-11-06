using System.Globalization;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Station;

/// <summary>
/// The station overlay (W07, design 10.8), drawn over the live world: the instruction strip across the top (its own small layer), the fixtures on the left, the targets in the
/// middle, the page of what can be held along the bottom with the empty hand and the page controls, the selection, the notice and the key hints. A noun is drawn by the colour rule
/// (grey until the word is used, then in colour); its English shows on hover until the noun is known (D-game-6). The picture of each cell is found by a key, and a key with no art
/// draws the plain tile.
/// </summary>
public sealed class StationLayer : IFrameLayer
{
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Unseen = UiPalette.Unseen.ToColor();
    private static readonly Color Known = new(255, 240, 190);
    private static readonly Color Muted = UiPalette.Muted.ToColor();
    private static readonly Color Cell = new(22, 24, 40, 245);
    private static readonly Color CellSelected = new(44, 58, 96, 250);
    private static readonly Color Right = new(120, 220, 130);
    private static readonly Color Wrong = new(240, 150, 100);
    private readonly InstructionStripLayer _strip = new();

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Station is not { IsOpen: true } panel)
        {
            return;
        }

        var model = panel.View();
        var layout = new StationLayout(view.ViewportWidth, view.ViewportHeight, model.Fixtures.Count, model.Targets.Count, model.Strip.Dots.Count, model.Verbs?.Verbs.Count ?? 0);
        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var ui = view.Session.Locale;
        spriteBatch.Draw(pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), new Color(6, 6, 14, 110)); // the world stays in sight
        var panelBox = Rect(layout.Panel);
        spriteBatch.Draw(pixel, panelBox, new Color(18, 18, 34, 246));
        DrawHelpers.Border(spriteBatch, pixel, panelBox, 2, Gold);

        DrawHelpers.Text(spriteBatch, font, ui.Ui("stationTitle"), new Vector2(layout.Title.X, layout.Title.Y + 2), Gold);
        _strip.Draw(spriteBatch, view, model.Strip, layout.Strip);

        for (var i = 0; i < layout.FixtureCount; i++)
        {
            DrawSide(spriteBatch, view, font, model.Fixtures[i], layout.Fixture(i));
        }

        for (var i = 0; i < model.Targets.Count; i++)
        {
            DrawTall(spriteBatch, view, font, model.Targets[i], layout.Target(i), layout.Compact ? 28 : 44);
        }

        DrawNotice(spriteBatch, view, font, model, layout);
        DrawHeldRow(spriteBatch, view, font, model, layout);
        DrawSelection(spriteBatch, view, font, model, layout);
        var hints = TextWrap.Wrap(ui.Ui("stationHints"), layout.Footer.Width, text => DrawHelpers.Width(font, text));
        for (var i = 0; i < Math.Min(2, hints.Count); i++)
        {
            DrawHelpers.Text(spriteBatch, font, hints[i], new Vector2(layout.Footer.X, layout.Footer.Y + (i * 20)), Muted);
        }

        if (model.Verbs is { } verbs)
        {
            DrawVerbs(spriteBatch, view, font, verbs, layout);
        }

        DrawHover(spriteBatch, view, font, model, layout);
    }

    private static Rectangle Rect(PixelBox box) => new(box.X, box.Y, box.Width, box.Height);

    private static Color Word(FrameView view, string noun) => Color.Lerp(Unseen, Known, (float)view.Session.Words.SaturationOf(noun));

    private static void DrawBox(SpriteBatch spriteBatch, FrameView view, Rectangle box, StationCell cell, bool hovered)
    {
        spriteBatch.Draw(view.Pixel, box, cell.Selected ? CellSelected : Cell);
        var edge = cell.Highlight ? Gold : cell.Selected ? Color.White : hovered ? new Color(190, 196, 220) : new Color(70, 70, 90);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, cell.Highlight || cell.Selected ? 3 : hovered ? 2 : 1, edge);
    }

    private static void DrawPicture(SpriteBatch spriteBatch, FrameView view, StationCell cell, Rectangle at)
    {
        if (cell.Item is { } item)
        {
            view.DrawIcon(spriteBatch, item, at);
        }
        else
        {
            view.VoxelIcons.DrawKey(spriteBatch, cell.IconKey, at, 1.0);
        }
    }

    // A fixture of the left column: the picture at the left, the noun beside it.
    private static void DrawSide(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, StationCell cell, PixelBox box)
    {
        var rectangle = Rect(box);
        DrawBox(spriteBatch, view, rectangle, cell with { Selected = false }, false);
        var size = Math.Min(32, box.Height - 6);
        DrawPicture(spriteBatch, view, cell, new Rectangle(box.X + 4, box.Y + ((box.Height - size) / 2), size, size));
        var textX = box.X + size + 10;
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, cell.Label, box.Right - 4 - textX), new Vector2(textX, box.Y + ((box.Height - 20) / 2)), Word(view, cell.Noun));
    }

    // A target: the picture above the noun.
    private static void DrawTall(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, StationCell cell, PixelBox box, int pictureSize)
    {
        var hovered = view.Mouse is { } mouse && box.Contains(mouse.X, mouse.Y);
        DrawBox(spriteBatch, view, Rect(box), cell, hovered);
        var size = Math.Min(pictureSize, box.Height - 26);
        DrawPicture(spriteBatch, view, cell, new Rectangle(box.X + ((box.Width - size) / 2), box.Y + 4, size, size));
        var label = DrawHelpers.Fit(font, cell.Label, box.Width - 8);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(box.X + ((box.Width - (int)DrawHelpers.Width(font, label)) / 2), box.Bottom - 22), Word(view, cell.Noun));
    }

    // A cell of the held row: the digit at its corner, the picture, the noun under it.
    private static void DrawHeld(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, StationCell cell, PixelBox box)
    {
        var hovered = view.Mouse is { } mouse && box.Contains(mouse.X, mouse.Y);
        DrawBox(spriteBatch, view, Rect(box), cell, hovered);
        var size = Math.Min(box.Width - 10, box.Height - 26);
        DrawPicture(spriteBatch, view, cell, new Rectangle(box.X + ((box.Width - size) / 2), box.Y + 3, size, size));
        if (cell.Key is { } key)
        {
            DrawHelpers.Text(spriteBatch, font, key.ToString(CultureInfo.InvariantCulture), new Vector2(box.X + 3, box.Y), Gold);
        }

        var label = DrawHelpers.Fit(font, cell.Label, box.Width - 4);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(box.X + 2, box.Bottom - 21), Word(view, cell.Noun));
    }

    private static void DrawHeldRow(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, StationView model, StationLayout layout)
    {
        var ui = view.Session.Locale;
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, ui.Ui("stationHeld"), layout.CaptionText.Width), new Vector2(layout.CaptionText.X, layout.CaptionText.Y + 1), Muted);
        if (model.PageCount > 1)
        {
            DrawControl(spriteBatch, view, font, layout.PagePrevious, "<", model.Page > 0);
            DrawControl(spriteBatch, view, font, layout.PageNext, ">", model.Page < model.PageCount - 1);
        }

        var page = ui.Ui("stationPage", ("page", (model.Page + 1).ToString(CultureInfo.InvariantCulture)), ("pages", model.PageCount.ToString(CultureInfo.InvariantCulture)));
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, page, layout.PageLabel.Width), new Vector2(layout.PageLabel.X + 4, layout.PageLabel.Y + 1), Known);
        DrawHeld(spriteBatch, view, font, model.Hand, layout.Hand);
        for (var i = 0; i < model.Held.Count; i++)
        {
            DrawHeld(spriteBatch, view, font, model.Held[i], layout.Held(i));
        }
    }

    private static void DrawControl(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, PixelBox box, string glyph, bool enabled)
    {
        spriteBatch.Draw(view.Pixel, Rect(box), enabled ? new Color(40, 44, 70, 245) : new Color(30, 30, 44, 235));
        DrawHelpers.Border(spriteBatch, view.Pixel, Rect(box), 1, enabled ? Gold : new Color(70, 70, 90));
        DrawHelpers.Text(spriteBatch, font, glyph, new Vector2(box.X + ((box.Width - (int)DrawHelpers.Width(font, glyph)) / 2), box.Y + 1), enabled ? Known : Muted);
    }

    private static void DrawSelection(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, StationView model, StationLayout layout)
    {
        var ui = view.Session.Locale;
        var text = model.HeldLabel is null
            ? ui.Ui("stationNothingHeld")
            : ui.Ui("stationSelection", ("held", model.HeldLabel), ("target", model.TargetLabel ?? "-"));
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, text, layout.Selection.Width), new Vector2(layout.Selection.X, layout.Selection.Y + 1), model.HeldLabel is null ? Muted : Known);
    }

    private static void DrawNotice(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, StationView model, StationLayout layout)
    {
        var ui = view.Session.Locale;
        (string Text, Color Colour)? line = model.Notice switch
        {
            StationNotice.Right => (ui.Ui("stationRight"), Right),
            StationNotice.WrongThing => (ui.Ui("stationWrongThing"), Wrong),
            StationNotice.WrongVerb => (ui.Ui("stationWrongVerb"), Wrong),
            StationNotice.OutOfOrder => (ui.Ui("stationOutOfOrder"), Wrong),
            StationNotice.Done => (ui.Ui("stationDone", ("count", model.Made.ToString(CultureInfo.InvariantCulture)), ("item", model.MadeLabel ?? "-")) + "  " + ui.Ui("stationAgain"), Right),
            StationNotice.BagFull => (ui.Ui("stationBagFull"), Wrong),
            StationNotice.Missing => (ui.Ui("stationMissing", ("nouns", string.Join(", ", model.Missing))), Wrong),
            _ => null,
        };
        if (line is { } shown)
        {
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, shown.Text, layout.Notice.Width), new Vector2(layout.Notice.X, layout.Notice.Y), shown.Colour);
        }
    }

    private static void DrawVerbs(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, VerbChoice choice, StationLayout layout)
    {
        var box = Rect(layout.VerbBox);
        spriteBatch.Draw(view.Pixel, box, new Color(14, 14, 28, 250));
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold);
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, view.Session.Locale.Ui("stationVerbs"), layout.VerbBox.Width - 16), new Vector2(layout.VerbBox.X + 8, layout.VerbBox.Y + 6), Muted);
        for (var i = 0; i < choice.Verbs.Count; i++)
        {
            var row = layout.Verb(i);
            var chosen = i == choice.Cursor;
            var hovered = view.Mouse is { } mouse && row.Contains(mouse.X, mouse.Y);
            spriteBatch.Draw(view.Pixel, Rect(row), chosen ? CellSelected : Cell);
            DrawHelpers.Border(spriteBatch, view.Pixel, Rect(row), chosen || hovered ? 2 : 1, chosen ? Color.White : new Color(70, 70, 90));
            DrawHelpers.Text(spriteBatch, font, $"{(i + 1).ToString(CultureInfo.InvariantCulture)}  {choice.Verbs[i]}", new Vector2(row.X + 8, row.Y + 3), Known);
        }
    }

    // The English of the noun under the pointer, until the noun is known (D-game-6): a small box beside the pointer, kept on the canvas.
    private static void DrawHover(SpriteBatch spriteBatch, FrameView view, SpriteFontBase font, StationView model, StationLayout layout)
    {
        if (view.Mouse is not { } mouse)
        {
            return;
        }

        var cells = new List<(StationCell Cell, PixelBox Box)>();
        for (var i = 0; i < layout.FixtureCount; i++)
        {
            cells.Add((model.Fixtures[i], layout.Fixture(i)));
        }

        for (var i = 0; i < model.Targets.Count; i++)
        {
            cells.Add((model.Targets[i], layout.Target(i)));
        }

        for (var i = 0; i < model.Held.Count; i++)
        {
            cells.Add((model.Held[i], layout.Held(i)));
        }

        cells.Add((model.Hand, layout.Hand));
        foreach (var (cell, box) in cells)
        {
            if (cell.Hover is not { Length: > 0 } english || !box.Contains(mouse.X, mouse.Y))
            {
                continue;
            }

            var width = (int)DrawHelpers.Width(font, english) + 12;
            var x = Math.Clamp(mouse.X + 12, 4, Math.Max(4, view.ViewportWidth - width - 4));
            var y = Math.Clamp(mouse.Y - 28, 4, Math.Max(4, view.ViewportHeight - 28));
            var tip = new Rectangle(x, y, width, 24);
            spriteBatch.Draw(view.Pixel, tip, new Color(10, 10, 20, 250));
            DrawHelpers.Border(spriteBatch, view.Pixel, tip, 1, Gold);
            DrawHelpers.Text(spriteBatch, font, english, new Vector2(x + 6, y + 2), Known);
            return;
        }
    }
}
