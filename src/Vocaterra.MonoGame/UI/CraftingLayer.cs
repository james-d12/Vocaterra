using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Graphics;

namespace Vocaterra.MonoGame.UI;

/// <summary>
/// The crafting panel (C): one row per recipe with the item's icon, its Spanish word, the ingredients as <c>have/need word</c>, the station
/// and a status at the right (<c>Hacer</c> when it can be made here, else what is missing). Every noun is drawn by the colour rule: grey until
/// the word is used, then in colour; seeing it counts as read once per opening (see <see cref="CraftingScreen"/>). A click on a row that
/// can be made sends one craft.
/// </summary>
public sealed class CraftingLayer : IFrameLayer
{
    /// <summary>The title over the rows.</summary>
    public const string Title = "Recipes";

    /// <summary>The key hints under the rows.</summary>
    public const string KeyHints = "C or Esc: close   click a row: make";

    private const int Icon = 32;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Unseen = UiPalette.Unseen.ToColor();
    private static readonly Color Known = new(255, 240, 190);
    private static readonly Color Hints = UiPalette.Muted.ToColor();
    private static readonly Color Enough = new(120, 220, 130);
    private static readonly Color Short = new(240, 110, 100);
    private static readonly Color Dim = new(30, 30, 44, 235);
    private static readonly Color Ready = new(24, 34, 30, 245);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.Crafting.IsOpen)
        {
            return;
        }

        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var viewport = new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight);
        spriteBatch.Draw(pixel, viewport, new Color(6, 6, 14, 150));

        var rows = CraftingScreen.Rows(view.Voxel, view.Session.Recipes);
        var layout = new CraftingLayout(viewport.Width, viewport.Height, rows.Count);
        var panel = new Rectangle(layout.X, layout.Y, layout.Width, layout.Height);
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);

        var left = layout.RowX;
        DrawHelpers.Text(spriteBatch, font, Title, new Vector2(left, panel.Top + 10), Gold);
        var made = rows.Count(static r => r.CanMake);
        var summary = $"You can make: {made.ToString(CultureInfo.InvariantCulture)} of {rows.Count.ToString(CultureInfo.InvariantCulture)}";
        DrawHelpers.Text(spriteBatch, font, summary, new Vector2(panel.Right - CraftingLayout.Padding - DrawHelpers.Width(font, summary), panel.Top + 10), Known);
        spriteBatch.Draw(pixel, new Rectangle(left, panel.Top + 36, layout.RowWidth, 1), new Color(255, 214, 64, 120));

        var hovered = view.Mouse is { } mouse ? layout.RowAt(mouse.X, mouse.Y) : null;
        foreach (var row in rows)
        {
            DrawRow(spriteBatch, view, font, layout, row, hovered == row.Index);
        }

        DrawHelpers.Text(spriteBatch, font, KeyHints, new Vector2(left, panel.Bottom - 26), Hints);
    }

    private static void DrawRow(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, CraftingLayout layout, CraftingRow row, bool hovered)
    {
        var pixel = view.Pixel;
        var box = new Rectangle(layout.RowX, layout.RowY(row.Index), layout.RowWidth, layout.RowHeight);
        spriteBatch.Draw(pixel, box, row.CanMake ? Ready : Dim);
        DrawHelpers.Border(spriteBatch, pixel, box, hovered && row.CanMake ? 2 : 1, row.CanMake ? (hovered ? Gold : Enough) : new Color(70, 70, 90));
        var middle = box.Center.Y - 8;
        var words = view.Session.Words;

        var output = row.Recipe.Output;
        var item = row.Recipe.Rule.Output;
        var size = Math.Min(Icon, box.Height - 6);
        view.DrawIcon(spriteBatch, item, new Rectangle(box.Left + 8, box.Center.Y - (size / 2), size, size));
        var x = box.Left + 8 + Icon + 10;
        var label = row.Recipe.OutputCount > 1
            ? $"{output.Lemma} x{row.Recipe.OutputCount.ToString(CultureInfo.InvariantCulture)}"
            : output.Lemma;
        x = DrawWord(spriteBatch, font, label, x, middle, words.SaturationOf(output.Lemma), 130);

        for (var i = 0; i < row.Recipe.Ingredients.Count; i++)
        {
            var part = row.Recipe.Ingredients[i];
            var amount = $"{row.Have[i].ToString(CultureInfo.InvariantCulture)}/{part.Count.ToString(CultureInfo.InvariantCulture)}";
            DrawHelpers.Text(spriteBatch, font, amount, new Vector2(x, middle), row.HasEnough(i) ? Enough : Short);
            var wordX = x + (int)DrawHelpers.Width(font, amount) + 6;
            x = DrawWord(spriteBatch, font, part.Noun.Lemma, wordX, middle, words.SaturationOf(part.Noun.Lemma), 0) + 14;
        }

        var status = row.Status;
        var statusWidth = (int)DrawHelpers.Width(font, status);
        var stationText = row.Recipe.Station is not null ? "Uses" : "By hand";
        var stationWord = row.Recipe.Station?.Lemma;
        var stationWidth = (int)DrawHelpers.Width(font, stationText) + (stationWord is null ? 0 : 34 - (int)DrawHelpers.Width(font, stationText) + (int)DrawHelpers.Width(font, stationWord));
        if (CraftingLayout.StationX(layout.RowWidth, x - box.Left, stationWidth, statusWidth) is { } offset)
        {
            var stationX = box.Left + offset;
            DrawHelpers.Text(spriteBatch, font, stationText, new Vector2(stationX, middle), Hints);
            if (stationWord is not null)
            {
                DrawWord(spriteBatch, font, stationWord, stationX + 34, middle, words.SaturationOf(stationWord), 0);
            }
        }

        DrawHelpers.Text(
            spriteBatch,
            font,
            status,
            new Vector2(box.Right - 10 - DrawHelpers.Width(font, status), middle),
            row.CanMake ? Enough : Short);
    }

    // A noun in its word colour (grey until used, then in colour); returns the x after it, or after at least minWidth.
    private static int DrawWord(SpriteBatch spriteBatch, FontStashSharp.SpriteFontBase font, string text, int x, int y, double saturation, int minWidth)
    {
        var colour = Color.Lerp(Unseen, Known, (float)saturation);
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(x, y), colour);
        return x + Math.Max((int)DrawHelpers.Width(font, text), minWidth);
    }
}
