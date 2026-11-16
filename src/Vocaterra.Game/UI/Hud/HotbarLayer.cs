using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// The hotbar: the first ten inventory slots in a strip at the bottom of the screen, the selected one outlined in gold, each stack's icon in
/// the colour of its word and its count. The number keys 1 to 0 and the mouse wheel select a slot.
/// </summary>
public sealed class HotbarLayer : IFrameLayer
{
    /// <summary>The width and height of one slot in pixels.</summary>
    public const int SlotSize = 48;

    /// <summary>The gap between two slots in pixels (the bubble meter lines up with the hotbar's edge).</summary>
    public const int Gap = 4;

    /// <summary>The distance from the canvas's bottom to the slots in pixels.</summary>
    public const int Bottom = 12;

    /// <summary>The hotbar's box on a canvas: ten slots in a strip, centred, <see cref="Bottom"/> up from the canvas's bottom (the needs' HUD and the layout tests place their parts against it).</summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    public static PixelBox BoxFor(int canvasWidth, int canvasHeight)
    {
        var width = (Core.Items.Inventory.HotbarSize * SlotSize) + ((Core.Items.Inventory.HotbarSize - 1) * Gap);
        return new PixelBox((canvasWidth - width) / 2, canvasHeight - SlotSize - Bottom, width, SlotSize);
    }

    /// <summary>The gap between the off-hand cell and the hotbar's first slot, wider than <see cref="Gap"/> so the cell reads as set apart (I04b).</summary>
    public const int OffHandGap = 10;

    /// <summary>The room the off-hand cell takes left of the hotbar: its width and its gap (the needs' HUD stands clear of it, I04b).</summary>
    public const int OffHandReserve = SlotSize + OffHandGap;

    /// <summary>The off-hand cell's box: one slot to the left of the hotbar's first, <see cref="OffHandGap"/> from it, at its height. It is drawn only while something is worn in the off-hand.</summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    public static PixelBox OffHandBoxFor(int canvasWidth, int canvasHeight)
    {
        var hotbar = BoxFor(canvasWidth, canvasHeight);
        return new PixelBox(hotbar.X - OffHandGap - SlotSize, hotbar.Y, SlotSize, SlotSize);
    }

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Frame = new(88, 92, 108);
    private static readonly Color Selected = new(250, 244, 220);
    private static readonly Color Back = new(22, 24, 30, 220);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var inventory = view.Items;
        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var viewport = new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight);
        var width = (Core.Items.Inventory.HotbarSize * SlotSize) + ((Core.Items.Inventory.HotbarSize - 1) * Gap);
        var left = (viewport.Width - width) / 2;
        var top = viewport.Height - SlotSize - Bottom;
        for (var i = 0; i < Core.Items.Inventory.HotbarSize; i++)
        {
            var box = new Rectangle(left + (i * (SlotSize + Gap)), top, SlotSize, SlotSize);
            var selected = i == view.SelectedSlot;
            // A dark rounded slot with a light edge, the selected one lifted by a bright frame (reference C).
            DrawHelpers.RoundedPanel(spriteBatch, pixel, box, 5, Back, selected ? Selected : Frame, selected ? 3 : 1);
            if (selected)
            {
                spriteBatch.Draw(pixel, new Rectangle(box.X + 5, box.Y + 3, box.Width - 10, 1), new Color(255, 255, 255, 60));
            }
            var stack = inventory.Slots[i];
            if (!stack.IsEmpty)
            {
                var icon = new Rectangle(box.X + 7, box.Y + 7, SlotSize - 14, SlotSize - 14);
                view.DrawIcon(spriteBatch, stack.Item, icon);
                if (CountBadge.Text(stack.Count, showOne: false) is { } text)
                {
                    DrawHelpers.Badge(spriteBatch, pixel, font, text, new Point(box.Right - 2, box.Bottom - 2));
                }
            }

            // The key that selects the slot: 1 to 9, then 0 for the tenth.
            var key = ((i + 1) % 10).ToString(CultureInfo.InvariantCulture);
            DrawHelpers.Text(spriteBatch, font, key, new Vector2(box.X + 3, box.Y + 1), selected ? Gold : UiPalette.HotbarKey.ToColor());
        }

        DrawOffHand(spriteBatch, view, font);
    }

    // The off-hand cell (I04b), as in Minecraft: one more cell left of the hotbar with the item worn in the off-hand slot, found by the slot's wear kind. Nothing worn there: nothing drawn. It is no hotbar slot (no key, no selection).
    private static void DrawOffHand(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font)
    {
        var stack = view.Voxel.Equipment.StackOfWear(Core.Items.Gear.AppearanceRoles.Default.OffHandWear);
        if (stack.IsEmpty)
        {
            return;
        }

        var cell = OffHandBoxFor(view.ViewportWidth, view.ViewportHeight);
        var box = new Rectangle(cell.X, cell.Y, cell.Width, cell.Height);
        DrawHelpers.RoundedPanel(spriteBatch, view.Pixel, box, 5, Back, Frame, 1);
        view.DrawIcon(spriteBatch, stack.Item, new Rectangle(box.X + 7, box.Y + 7, SlotSize - 14, SlotSize - 14));
        if (CountBadge.Text(stack.Count, showOne: false) is { } text)
        {
            DrawHelpers.Badge(spriteBatch, view.Pixel, font, text, new Point(box.Right - 2, box.Bottom - 2));
        }
    }
}
