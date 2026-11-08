using Vocaterra.Game.Core.Water;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// Where the bubble meter stands on a canvas (H04): ten bubbles in a row above the right half of the hotbar, right-aligned to the hotbar's right edge, as in
/// Minecraft. Plain numbers, so a test places them on the smallest canvas the layouts are proven on (533 x 400, UI scale 150 percent) and on the usual ones.
/// The <c>raise</c> argument lifts the row above whatever stands over the hotbar (H11 passes the height of the needs bars).
/// </summary>
public static class BreathHudLayout
{
    /// <summary>The side of one bubble's box in canvas pixels.</summary>
    public const int BubbleSize = 14;

    /// <summary>The gap between two bubbles.</summary>
    public const int BubbleGap = 3;

    /// <summary>The gap between the row and the top of the hotbar.</summary>
    public const int Lift = 4;

    /// <summary>The bubbles the meter has in a row (one per tenth of the air).</summary>
    public const int Count = Breath.MaxBubbles;

    /// <summary>The ten bubble boxes, left to right, on a canvas of the given size.</summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    /// <param name="raise">How far the row sits above its place over the hotbar, in canvas pixels (0 for none).</param>
    public static IReadOnlyList<PixelBox> Bubbles(int canvasWidth, int canvasHeight, int raise = 0)
    {
        var hotbarWidth = (Core.Items.Inventory.HotbarSize * HotbarLayer.SlotSize) + ((Core.Items.Inventory.HotbarSize - 1) * HotbarLayer.Gap);
        var hotbarRight = ((canvasWidth - hotbarWidth) / 2) + hotbarWidth;
        var rowWidth = (Count * BubbleSize) + ((Count - 1) * BubbleGap);
        var left = hotbarRight - rowWidth;
        var top = canvasHeight - HudLayout.HotbarTop - Lift - BubbleSize - raise;

        var boxes = new PixelBox[Count];
        for (var i = 0; i < Count; i++)
        {
            boxes[i] = new PixelBox(left + (i * (BubbleSize + BubbleGap)), top, BubbleSize, BubbleSize);
        }

        return boxes;
    }
}
