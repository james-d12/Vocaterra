using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Items;

namespace Vocaterra.MonoGame;

/// <summary>
/// The hotbar: the first ten inventory slots in a strip at the bottom of the screen, the selected one outlined in gold, each stack's icon in
/// the colour of its word and its count. The number keys 1 to 0 and the mouse wheel select a slot.
/// </summary>
public sealed class HotbarLayer : IFrameLayer
{
    /// <summary>The width and height of one slot in pixels.</summary>
    public const int SlotSize = 40;

    private const int Gap = 4;
    private const int Bottom = 12;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Frame = new(90, 90, 110);
    private static readonly Color Back = new(10, 10, 20, 200);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var inventory = view.Items;
        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var viewport = new Rectangle(0, 0, view.Camera.ViewportWidth, view.Camera.ViewportHeight);
        var width = (Inventory.HotbarSize * SlotSize) + ((Inventory.HotbarSize - 1) * Gap);
        var left = (viewport.Width - width) / 2;
        var top = viewport.Height - SlotSize - Bottom;
        for (var i = 0; i < Inventory.HotbarSize; i++)
        {
            var box = new Rectangle(left + (i * (SlotSize + Gap)), top, SlotSize, SlotSize);
            spriteBatch.Draw(pixel, box, Back);
            var selected = i == view.SelectedSlot;
            DrawHelpers.Border(spriteBatch, pixel, box, selected ? 3 : 1, selected ? Gold : Frame);
            var stack = inventory.Slots[i];
            if (!stack.IsEmpty)
            {
                var icon = new Rectangle(box.X + 6, box.Y + 6, SlotSize - 12, SlotSize - 12);
                view.DrawIcon(spriteBatch, stack.Item, icon);
                if (CountBadge.Text(stack.Count, showOne: false) is { } text)
                {
                    DrawHelpers.Badge(spriteBatch, pixel, font, text, new Point(box.Right - 2, box.Bottom - 2));
                }
            }

            // The key that selects the slot: 1 to 9, then 0 for the tenth.
            var key = ((i + 1) % 10).ToString(CultureInfo.InvariantCulture);
            DrawHelpers.Text(spriteBatch, font, key, new Vector2(box.X + 3, box.Y + 1), selected ? Gold : Frame);
        }
    }
}
