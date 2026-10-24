using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Graphics;
using Vocaterra.Simulation.Items;

namespace Vocaterra.MonoGame.UI;

/// <summary>
/// The inventory screen (I or E): the 40 slots in four rows of ten (the top row is the hotbar), each with its item's icon, the item's Spanish
/// word (<c>madera</c>) and the count. The icon and the word are coloured by the word's saturation (grey until it is used, then in colour), and
/// the words count as read when the screen opens (see <see cref="InventoryScreen"/>). It covers the screen while it is open.
/// </summary>
public sealed class InventoryLayer : IFrameLayer
{
    /// <summary>The title over the slots.</summary>
    public const string Title = "Inventory";

    /// <summary>The key hints when the full line does not fit the panel (X08).</summary>
    public const string KeyHintsShort = "I or Esc: close   1 to 0 or wheel: choose   click: mine, place";

    /// <summary>The key hints under the slots.</summary>
    public const string KeyHints = "I or Esc: close   1 to 0 or wheel: choose   left click: mine   right click: place";

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

        var layout = new InventoryLayout(viewport.Width, viewport.Height, Inventory.SlotCount);
        var panel = new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, layout.Panel.Height);
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);

        var left = panel.Left + InventoryLayout.Padding;
        var textWidth = panel.Width - (InventoryLayout.Padding * 2);
        DrawHelpers.Text(spriteBatch, font, Title, new Vector2(left, panel.Top + 10), Gold);
        var used = view.Items.Slots.Count(static s => !s.IsEmpty);
        var summary = $"Slots used: {used} of {Inventory.SlotCount}";
        DrawHelpers.Text(spriteBatch, font, summary, new Vector2(panel.Right - InventoryLayout.Padding - DrawHelpers.Width(font, summary), panel.Top + 10), Known);
        spriteBatch.Draw(pixel, new Rectangle(left, panel.Top + 36, textWidth, 1), new Color(255, 214, 64, 120));

        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            var cell = layout.Cell(slot);
            DrawSlot(spriteBatch, view, font, new Rectangle(cell.X, cell.Y, cell.Width, cell.Height), view.Items.Slots[slot], layout.Compact);
        }

        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, DrawHelpers.Width(font, KeyHints) <= textWidth ? KeyHints : KeyHintsShort, textWidth), new Vector2(left, panel.Bottom - 26), Hints);
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

        var noun = view.Session.Nouns.ForItem(stack.Item);
        var label = noun?.Lemma ?? ItemDefinition.Get(stack.Item).Name;
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
