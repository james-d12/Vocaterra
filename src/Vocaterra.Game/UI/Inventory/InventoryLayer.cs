using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// The inventory screen (I or E): the 40 slots in four rows of ten (the top row is the hotbar), each with its item's icon, the item's Spanish
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
        var layout = new InventoryLayout(viewport.Width, viewport.Height, Core.Items.Inventory.SlotCount, needs.Count);
        var panel = new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, layout.Panel.Height);
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);

        var left = panel.Left + InventoryLayout.Padding;
        var textWidth = panel.Width - (InventoryLayout.Padding * 2);
        var ui = view.Session.Locale;
        DrawHelpers.Text(spriteBatch, font, ui.Ui("inventoryTitle"), new Vector2(left, panel.Top + 10), Gold);
        var used = view.Items.Slots.Count(static s => !s.IsEmpty);
        var summary = ui.Ui("inventoryUsed", ("used", used.ToString(CultureInfo.InvariantCulture)), ("slots", Core.Items.Inventory.SlotCount.ToString(CultureInfo.InvariantCulture)));
        DrawHelpers.Text(spriteBatch, font, summary, new Vector2(panel.Right - InventoryLayout.Padding - DrawHelpers.Width(font, summary), panel.Top + 10), Known);
        spriteBatch.Draw(pixel, new Rectangle(left, panel.Top + layout.DividerY, textWidth, 1), new Color(255, 214, 64, 120));

        for (var slot = 0; slot < Core.Items.Inventory.SlotCount; slot++)
        {
            var cell = layout.Cell(slot);
            DrawSlot(spriteBatch, view, font, new Rectangle(cell.X, cell.Y, cell.Width, cell.Height), view.Items.Slots[slot], layout.Compact);
        }

        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, DrawHelpers.Width(font, ui.Ui("inventoryHints")) <= textWidth ? ui.Ui("inventoryHints") : ui.Ui("inventoryHintsShort"), textWidth), new Vector2(left, panel.Bottom - layout.HintsOffset), Hints);
        DrawNeeds(spriteBatch, view, font, layout, needs, left, textWidth);
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

        var label = view.Session.Nouns.LabelOf(stack.Item) ?? ItemDefinition.Get(stack.Item).Name;
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
