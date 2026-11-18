using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// The inventory screen, now the equipment panel (I or E; I04): the paper doll on the left, a stats line under it, and the bag on the right: the active slots (30, and a worn bag's more, I03b) in three to five rows of ten (the top row is the hotbar), each with its item's icon, the item's Spanish
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
    private static readonly Color Refusal = new(255, 140, 110);
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
        var equipment = view.Voxel.Equipment;
        var layout = new InventoryLayout(viewport.Width, viewport.Height, view.Items.SlotCount, needs.Count, equipment.Data.Shape);
        var panel = new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, layout.Panel.Height);
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);

        var left = layout.ContentLeft;
        var textWidth = layout.ContentWidth;
        var ui = view.Session.Locale;
        DrawTitle(spriteBatch, view, font, ui.Ui("inventoryTitle"), left, panel.Top + 10);
        var used = view.Items.Slots.Count(static s => !s.IsEmpty);
        var summary = ui.Ui("inventoryUsed", ("used", used.ToString(CultureInfo.InvariantCulture)), ("slots", view.Items.SlotCount.ToString(CultureInfo.InvariantCulture)));
        DrawHelpers.Text(spriteBatch, font, summary, new Vector2(panel.Right - InventoryLayout.Padding - DrawHelpers.Width(font, summary), panel.Top + 10), Known);
        spriteBatch.Draw(pixel, new Rectangle(left, panel.Top + layout.DividerY, textWidth, 1), new Color(255, 214, 64, 120));

        var hit = view.Mouse is { } pointer ? layout.Hit(pointer.X, pointer.Y, view.Items.SlotCount) : InventoryHit.None;
        DrawDoll(spriteBatch, view, font, layout, hit);
        for (var slot = 0; slot < view.Items.SlotCount; slot++) // the active slots only: three to five rows of ten (I03b)
        {
            var cell = layout.Cell(slot);
            DrawSlot(spriteBatch, view, font, new Rectangle(cell.X, cell.Y, cell.Width, cell.Height), view.Items.Slots[slot], layout.Compact, layout.Tight, hit is { Kind: InventoryHitKind.Cell } over && over.Slot == slot);
        }

        DrawSortButton(spriteBatch, view, layout.SortButton);
        DrawTrash(spriteBatch, view, layout.TrashCell);
        DrawFooter(spriteBatch, view, font, layout, hit, left, textWidth);
        DrawNeeds(spriteBatch, view, font, layout, needs, left, textWidth);
        DrawHeld(spriteBatch, view, font, layout);
    }

    // The title (I04b): the Spanish noun with its article, «la bolsa» (D-game-35 (b)), and the English beside it, dimmer, as the needs list writes its Spanish and English.
    private static void DrawTitle(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, string english, int x, int y)
    {
        var spanish = view.Session.Nouns.ForLemma(InventoryPanel.TitleLemma)?.WithDefinite;
        if (spanish is null)
        {
            DrawHelpers.Text(spriteBatch, font, english, new Vector2(x, y), Gold);
            return;
        }

        DrawHelpers.Text(spriteBatch, font, spanish, new Vector2(x, y), Gold);
        DrawHelpers.Text(spriteBatch, font, english, new Vector2(x + DrawHelpers.Width(font, spanish) + 12, y), Dialogue.DialoguePanel.EnglishColor);
    }

    // The paper doll (I04): the body rows' nouns, the cells (the worn item's icon, or the slot's noun while it is empty) and the stats line.
    private static void DrawDoll(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, InventoryLayout layout, InventoryHit hit)
    {
        var shape = layout.Shape;
        if (shape.IsEmpty)
        {
            return;
        }

        var pixel = view.Pixel;
        var equipment = view.Voxel.Equipment;
        var nouns = view.Session.Nouns;
        for (var column = 0; column < shape.ColumnHeadings.Count; column++)
        {
            if (shape.ColumnHeadings[column] is { } lemma)
            {
                var box = layout.HeadingBox(column);
                var text = HeadingText.Pick(lemma, box.Width, t => DrawHelpers.Width(font, t));
                var colour = Color.Lerp(Unseen, Known, (float)view.Session.Words.SaturationOf(lemma));
                DrawHelpers.Text(spriteBatch, font, text, new Vector2(layout.HeadingTextLeft(column, (int)Math.Ceiling(DrawHelpers.Width(font, text))), box.Y + ((box.Height - 16) / 2)), colour);
            }
        }

        if (layout.HasLabelColumn)
        {
            for (var row = 0; row < shape.BodyRows; row++)
            {
                if (shape.RowLabels[row] is { } lemma)
                {
                    var box = layout.RowLabelBox(row);
                    var text = DrawHelpers.Fit(font, nouns.ForLemma(lemma)?.WithDefinite ?? lemma, box.Width);
                    var colour = Color.Lerp(Unseen, Known, (float)view.Session.Words.SaturationOf(lemma));
                    DrawHelpers.Text(spriteBatch, font, text, new Vector2(box.X, box.Y + ((box.Height - 16) / 2)), colour);
                }
            }
        }

        foreach (var cell in shape.Cells)
        {
            var box = layout.EquipmentCellBox(cell);
            var rect = new Rectangle(box.X, box.Y, box.Width, box.Height);
            var stack = equipment.Slots[cell.Flat];
            var hot = hit is { Kind: InventoryHitKind.Equipment } over && over.Slot == cell.Flat;
            spriteBatch.Draw(pixel, rect, stack.IsEmpty ? Empty : Filled);
            DrawHelpers.Border(spriteBatch, pixel, rect, 1, hot ? Gold : new Color(70, 70, 90));
            if (!stack.IsEmpty)
            {
                var size = Math.Min(Icon, box.Width - 4);
                view.DrawIcon(spriteBatch, stack.Item, new Rectangle(box.X + ((box.Width - size) / 2), box.Y + ((box.Height - size) / 2), size, size));
            }
            else if (layout.HasLabelColumn && cell.Row >= shape.BodyRows && cell.Label is { } lemma)
            {
                var text = DrawHelpers.Fit(font, lemma, box.Width - 2); // a counted slot with a noun writes it in the empty cell, which is wide enough for the whole word (the body rows' nouns stand beside the doll)
                DrawHelpers.Text(spriteBatch, font, text, new Vector2(box.X + ((box.Width - DrawHelpers.Width(font, text)) / 2), box.Y + ((box.Height - 16) / 2)), Hints);
            }
        }

        DrawStats(spriteBatch, view, font, layout);
    }

    // The stats line (I04): each stat that is not at its default as an icon and a number, the Spanish noun before it where the game has one and the room allows.
    private static void DrawStats(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, InventoryLayout layout)
    {
        var nouns = view.Session.Nouns;
        var chips = StatDisplay.Chips(view.Voxel.Stats, hasNoun: lemma => nouns.ForLemma(lemma) is not null);
        if (chips.Count == 0)
        {
            return;
        }

        var iconSize = layout.StatChipHeight - 2;
        var small = layout.Tight || layout.Compact;
        var widths = chips.Select(chip => iconSize + 4 + (int)DrawHelpers.Width(font, ChipText(chip, small)) + (small ? 4 : 8)).ToList();
        var boxes = StatLinePlacer.Place(widths, layout.StatsArea, layout.StatChipHeight, 0);
        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            StatIcons.Draw(spriteBatch, view.Pixel, chips[i].Key, new Rectangle(box.X, box.Y + 1, iconSize, iconSize), Known);
            DrawHelpers.Text(spriteBatch, font, ChipText(chips[i], layout.Tight || layout.Compact), new Vector2(box.X + iconSize + 4, box.Y), Known);
        }
    }

    // The regular size writes the stat's Spanish noun before its number; the small sizes keep the icon and the number.
    private static string ChipText(StatChip chip, bool small) => chip.Lemma is { } lemma && !small ? lemma + " " + chip.Value : chip.Value;

    // The line under the slots: the refusal that just happened, else what the pointer is on, else the key hints.
    private static void DrawFooter(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, InventoryLayout layout, InventoryHit hit, int left, int textWidth)
    {
        var ui = view.Session.Locale;
        var origin = new Vector2(left, layout.Panel.Bottom - layout.HintsOffset);
        if ((view.Session as VoxelSession)?.MoveNotice is { } notice && notice.IsShown(view.Voxel.Tick))
        {
            var spanish = view.Session.Data.Words.HudLine(notice.Key) ?? ui.Text(notice.Key);
            var spanishWidth = DrawHelpers.Width(font, spanish);
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, spanish, textWidth), origin, Refusal);
            if (spanishWidth + 12 < textWidth) // the English beside it, dimmer, as the needs rows have it
            {
                DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, ui.Text(notice.Key), textWidth - (int)spanishWidth - 12), new Vector2(left + spanishWidth + 12, origin.Y), Dialogue.DialoguePanel.EnglishColor);
            }

            return;
        }

        var session = view.Session;
        if (InventoryHover.Of(hit, view.Items, view.Voxel.Equipment, session.Nouns, session.Words, ui.NounSupport) is { IsEmpty: false } hover)
        {
            var spanish = hover.Slot is null ? hover.Item ?? string.Empty : hover.Item is null ? hover.Slot : hover.Slot + ": " + hover.Item;
            var spanishWidth = DrawHelpers.Width(font, spanish);
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, spanish, textWidth), origin, Color.Lerp(Unseen, Known, (float)hover.Saturation));
            if (hover.English is { } support && spanishWidth + 12 < textWidth)
            {
                DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, support, textWidth - (int)spanishWidth - 12), new Vector2(left + spanishWidth + 12, origin.Y), Dialogue.DialoguePanel.EnglishColor);
            }

            return;
        }

        var hints = DrawHelpers.Width(font, ui.Ui("inventoryHints")) <= textWidth ? ui.Ui("inventoryHints") : ui.Ui("inventoryHintsShort");
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, hints, textWidth), origin, Hints);
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

    // The held stack (I02) at the pointer, or beside the cell it came from when there is no pointer (I04b): the icon and the count, as the cells draw them.
    private static void DrawHeld(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, InventoryLayout layout)
    {
        var held = view.Voxel.Held;
        if (held.Stack.IsEmpty)
        {
            return;
        }

        var origin = HeldPlacement.OriginCell(layout, held.OriginContainer, held.OriginSlot, view.Items.SlotCount);
        var box = HeldPlacement.Icon(view.Mouse, origin, Icon, view.ViewportWidth, view.ViewportHeight);
        var icon = new Rectangle(box.X, box.Y, box.Width, box.Height);
        view.DrawIcon(spriteBatch, held.Stack.Item, icon);
        if (CountBadge.Text(held.Stack.Count, showOne: true) is { } count)
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

    private static void DrawSlot(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, Rectangle box, ItemStack stack, bool compact, bool tight, bool hot)
    {
        var pixel = view.Pixel;
        spriteBatch.Draw(pixel, box, stack.IsEmpty ? Empty : Filled);
        DrawHelpers.Border(spriteBatch, pixel, box, 1, hot ? Gold : new Color(70, 70, 90));
        if (stack.IsEmpty)
        {
            return;
        }

        var saturation = view.Session.Words.SaturationOf(stack.Item);
        var icon = new Rectangle(box.Center.X - (Icon / 2), box.Top + (tight ? 2 : compact ? 3 : 4), Icon, Icon);
        view.DrawIcon(spriteBatch, stack.Item, icon);

        var label = view.Session.Nouns.LabelOf(stack.Item) ?? view.Session.Data.Items.Get(stack.Item).Name;
        var colour = Color.Lerp(Unseen, Known, (float)saturation);
        label = DrawHelpers.Fit(font, label, box.Width - 2); // a narrow slot cuts a long name with an ellipsis (X08)
        var labelWidth = DrawHelpers.Width(font, label);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(box.Center.X - (labelWidth / 2), box.Top + (tight ? 34 : compact ? 36 : 40)), colour);

        if (CountBadge.Text(stack.Count, showOne: true) is { } count)
        {
            DrawHelpers.Badge(spriteBatch, pixel, font, count, new Point(box.Right - 3, compact ? box.Top + (tight ? 34 : 36) : box.Bottom - 3)); // a short slot puts the count over the icon's corner, clear of the name (X08)
        }
    }
}
