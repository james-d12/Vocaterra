using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Items;

namespace Vocaterra.MonoGame;

/// <summary>
/// The inventory screen (I or E): the 40 slots in four rows of ten (the top row is the hotbar), each with its item's icon, the item's Spanish
/// word (<c>madera</c>) and the count. The icon and the word are coloured by the word's saturation (grey until it is used, then in colour), and
/// the words count as read when the screen opens (see <see cref="InventoryScreen"/>). It covers the screen while it is open.
/// </summary>
public sealed class InventoryLayer : IFrameLayer
{
    /// <summary>The title over the slots.</summary>
    public const string Title = "Inventario";

    /// <summary>The key hints under the slots.</summary>
    public const string KeyHints = "I o Esc: cerrar   1 a 0 o rueda: elegir   clic izq.: minar   clic der.: colocar";

    private const int Columns = Inventory.HotbarSize;
    private const int Cell = 64;
    private const int CellHeight = 80;
    private const int Gap = 6;
    private const int Padding = 16;
    private const int Icon = 32;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Unseen = new(150, 150, 165);
    private static readonly Color Known = new(255, 240, 190);
    private static readonly Color Hints = new(140, 140, 150);
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

        var rows = Inventory.SlotCount / Columns;
        var width = Math.Min((Columns * Cell) + ((Columns - 1) * Gap) + (Padding * 2), viewport.Width - 32);
        var height = (Padding * 2) + 36 + (rows * CellHeight) + ((rows - 1) * Gap) + 34;
        var panel = new Rectangle((viewport.Width - width) / 2, Math.Max(8, (viewport.Height - height) / 2), width, height);
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);

        var left = panel.Left + Padding;
        var textWidth = panel.Width - (Padding * 2);
        DrawHelpers.Text(spriteBatch, font, Title, new Vector2(left, panel.Top + 10), Gold);
        var used = view.Items.Slots.Count(static s => !s.IsEmpty);
        var summary = $"Ocupadas: {used} de {Inventory.SlotCount}";
        DrawHelpers.Text(spriteBatch, font, summary, new Vector2(panel.Right - Padding - DrawHelpers.Width(font, summary), panel.Top + 10), Known);
        spriteBatch.Draw(pixel, new Rectangle(left, panel.Top + 36, textWidth, 1), new Color(255, 214, 64, 120));

        var gridLeft = left + ((textWidth - ((Columns * Cell) + ((Columns - 1) * Gap))) / 2);
        var gridTop = panel.Top + 44;
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            var row = slot / Columns;
            var column = slot % Columns;
            var box = new Rectangle(gridLeft + (column * (Cell + Gap)), gridTop + (row * (CellHeight + Gap)), Cell, CellHeight);
            DrawSlot(spriteBatch, view, font, box, view.Items.Slots[slot]);
        }

        DrawHelpers.Text(spriteBatch, font, KeyHints, new Vector2(left, panel.Bottom - 26), Hints);
    }

    private static void DrawSlot(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, Rectangle box, ItemStack stack)
    {
        var pixel = view.Pixel;
        spriteBatch.Draw(pixel, box, stack.IsEmpty ? Empty : Filled);
        DrawHelpers.Border(spriteBatch, pixel, box, 1, new Color(70, 70, 90));
        if (stack.IsEmpty)
        {
            return;
        }

        var saturation = view.Session.Words.SaturationOf(stack.Item);
        var icon = new Rectangle(box.Center.X - (Icon / 2), box.Top + 4, Icon, Icon);
        view.DrawIcon(spriteBatch, stack.Item, icon);

        var noun = view.Session.Nouns.ForItem(stack.Item);
        var label = noun?.Lemma ?? ItemDefinition.Get(stack.Item).Name;
        var colour = Color.Lerp(Unseen, Known, (float)saturation);
        var labelWidth = DrawHelpers.Width(font, label);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(box.Center.X - (labelWidth / 2), box.Top + 40), colour);

        if (CountBadge.Text(stack.Count, showOne: true) is { } count)
        {
            DrawHelpers.Badge(spriteBatch, pixel, font, count, new Point(box.Right - 3, box.Bottom - 3));
        }
    }
}
