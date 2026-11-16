using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// The inventory screen (I or E): the active slots (30, and a worn bag's more, I03b) in three to five rows of ten (the top row is the hotbar), each with its item's icon, the item's Spanish
/// word (<c>madera</c>) and the count. The icon and the word are coloured by the word's saturation (grey until it is used, then in colour), and
/// the words count as read when the screen opens (see <see cref="InventoryScreen"/>). It covers the screen while it is open.
/// </summary>
public sealed class InventoryLayer : IFrameLayer
{

    private const int Icon = 32;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Unseen = UiPalette.Unseen.ToColor();
    private static readonly Color Known = new(255, 240, 190);
    private static readonly Color Hints = UiPalette.Muted.ToColor();
    private static readonly Color Empty = new(30, 30, 44, 230);
    private static readonly Color Filled = new(16, 16, 30, 240);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.Inventory.IsOpen)
        {
            return;
        }

        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var viewport = new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight);
        spriteBatch.Draw(pixel, viewport, new Color(6, 6, 14, 150));

        IReadOnlyList<NeedsListRow> needs = view.Needs is { Visible: true } hud ? hud.ListRows() : [];
        var layout = new InventoryLayout(viewport.Width, viewport.Height, view.Items.SlotCount, needs.Count);
        var panel = new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, layout.Panel.Height);
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);

        var left = panel.Left + InventoryLayout.Padding;
        var textWidth = panel.Width - (InventoryLayout.Padding * 2);
        var ui = view.Session.Locale;
        DrawHelpers.Text(spriteBatch, font, ui.Ui("inventoryTitle"), new Vector2(left, panel.Top + 10), Gold);
        var used = view.Items.Slots.Count(static s => !s.IsEmpty);
        var summary = ui.Ui("inventoryUsed", ("used", used.ToString(CultureInfo.InvariantCulture)), ("slots", view.Items.SlotCount.ToString(CultureInfo.InvariantCulture)));
        DrawHelpers.Text(spriteBatch, font, summary, new Vector2(layout.SortButton.X - 10 - DrawHelpers.Width(font, summary), panel.Top + 10), Known);
        spriteBatch.Draw(pixel, new Rectangle(left, panel.Top + layout.DividerY, textWidth, 1), new Color(255, 214, 64, 120));

        for (var slot = 0; slot < view.Items.SlotCount; slot++) // the active slots only: three to five rows of ten (I03b)
        {
            var cell = layout.Cell(slot);
            DrawSlot(spriteBatch, view, font, new Rectangle(cell.X, cell.Y, cell.Width, cell.Height), view.Items.Slots[slot], layout.Compact);
        }

        DrawSortButton(spriteBatch, view, layout.SortButton);
        DrawTrash(spriteBatch, view, layout.TrashCell);
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, DrawHelpers.Width(font, ui.Ui("inventoryHints")) <= textWidth ? ui.Ui("inventoryHints") : ui.Ui("inventoryHintsShort"), textWidth), new Vector2(left, panel.Bottom - layout.HintsOffset), Hints);
        DrawNeeds(spriteBatch, view, font, layout, needs, left, textWidth);
        DrawHeld(spriteBatch, view, font);
    }

    // The sort button (I02): three bars that shorten, an icon only (the words are I04's).
    private static void DrawSortButton(SpriteBatch spriteBatch, FrameView view, PixelBox box)
    {
        var pixel = view.Pixel;
        var rect = new Rectangle(box.X, box.Y, box.Width, box.Height);
        var hot = view.Mouse is { } at && box.Contains(at.X, at.Y);
        spriteBatch.Draw(pixel, rect, hot ? new Color(48, 48, 70) : Filled);
        DrawHelpers.Border(spriteBatch, pixel, rect, 1, hot ? Gold : new Color(70, 70, 90));
        var bar = Math.Max(2, box.Height / 7);
        for (var i = 0; i < 3; i++)
        {
            var width = (box.Width - 8) * (3 - i) / 3;
            spriteBatch.Draw(pixel, new Rectangle(box.X + 4, box.Y + 4 + (i * (bar + 3)), Math.Max(width, 4), bar), Known);
        }
    }

    // The trash cell (I02): a bin drawn from rectangles, or the stack it holds (it keeps its last item until another replaces it).
    private static void DrawTrash(SpriteBatch spriteBatch, FrameView view, PixelBox box)
    {
        var pixel = view.Pixel;
        var rect = new Rectangle(box.X, box.Y, box.Width, box.Height);
        var hot = view.Mouse is { } at && box.Contains(at.X, at.Y);
        spriteBatch.Draw(pixel, rect, hot ? new Color(48, 48, 70) : Filled);
        DrawHelpers.Border(spriteBatch, pixel, rect, 1, hot ? Gold : new Color(70, 70, 90));
        var stack = view.Voxel.Trash.Stack;
        if (!stack.IsEmpty)
        {
            var size = Math.Max(8, box.Width - 6);
            view.DrawIcon(spriteBatch, stack.Item, new Rectangle(box.X + 3, box.Y + 3, size, size));
            return;
        }

        var inner = box.Width - 10;
        spriteBatch.Draw(pixel, new Rectangle(box.X + 4, box.Y + 5, box.Width - 8, 2), Hints); // the lid
        spriteBatch.Draw(pixel, new Rectangle(box.X + 5 + (inner / 3), box.Y + 3, Math.Max(2, inner / 3), 2), Hints); // its handle
        spriteBatch.Draw(pixel, new Rectangle(box.X + 6, box.Y + 8, box.Width - 12, Math.Max(3, box.Height - 13)), Hints); // the bin
    }

    // The held stack (I02) at the pointer: the icon and the count, as the cells draw them.
    private static void DrawHeld(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font)
    {
        var held = view.Voxel.Held.Stack;
        if (held.IsEmpty || view.Mouse is not { } at)
        {
            return;
        }

        var icon = new Rectangle(at.X - (Icon / 2), at.Y - (Icon / 2), Icon, Icon);
        view.DrawIcon(spriteBatch, held.Item, icon);
        if (CountBadge.Text(held.Count, showOne: true) is { } count)
        {
            DrawHelpers.Badge(spriteBatch, view.Pixel, font, count, new Point(icon.Right + 2, icon.Bottom + 2));
        }
    }

    // The five needs under the slots (N05): the need's name in Spanish, its stage as the player's own line, and the English.
    private static void DrawNeeds(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, InventoryLayout layout, IReadOnlyList<NeedsListRow> needs, int left, int textWidth)
    {
        if (needs.Count == 0)
        {
            return;
        }

        var nameWidth = Math.Min(96, textWidth / 5);
        var spanishWidth = Math.Min(230, (textWidth - nameWidth) / 2);
        for (var i = 0; i < needs.Count; i++)
        {
            var row = needs[i];
            var y = layout.NeedsTop + (i * layout.NeedsRowStep);
            var worse = row.Stage >= 3 ? new Color(255, 150, 90) : Known;
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, row.Name, nameWidth - 6), new Vector2(left, y), Gold);
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, row.Spanish, spanishWidth - 6), new Vector2(left + nameWidth, y), row.Stage == 0 ? Hints : worse);
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, row.English, textWidth - nameWidth - spanishWidth), new Vector2(left + nameWidth + spanishWidth, y), Dialogue.DialoguePanel.EnglishColor);
        }
    }

    private static void DrawSlot(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, Rectangle box, ItemStack stack, bool compact)
    {
        var pixel = view.Pixel;
        spriteBatch.Draw(pixel, box, stack.IsEmpty ? Empty : Filled);
        DrawHelpers.Border(spriteBatch, pixel, box, 1, new Color(70, 70, 90));
        if (stack.IsEmpty)
        {
            return;
        }

        var saturation = view.Session.Words.SaturationOf(stack.Item);
        var icon = new Rectangle(box.Center.X - (Icon / 2), box.Top + (compact ? 3 : 4), Icon, Icon);
        view.DrawIcon(spriteBatch, stack.Item, icon);

        var label = view.Session.Nouns.LabelOf(stack.Item) ?? view.Session.Data.Items.Get(stack.Item).Name;
        var colour = Color.Lerp(Unseen, Known, (float)saturation);
        label = DrawHelpers.Fit(font, label, box.Width - 2); // a narrow slot cuts a long name with an ellipsis (X08)
        var labelWidth = DrawHelpers.Width(font, label);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(box.Center.X - (labelWidth / 2), box.Top + (compact ? 36 : 40)), colour);

        if (CountBadge.Text(stack.Count, showOne: true) is { } count)
        {
            DrawHelpers.Badge(spriteBatch, pixel, font, count, new Point(box.Right - 3, compact ? box.Top + 36 : box.Bottom - 3)); // a short slot puts the count over the icon's corner, clear of the name (X08)
        }
    }
}
