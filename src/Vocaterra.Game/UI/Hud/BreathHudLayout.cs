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

    /// <summary>
    /// How far the bubble row must be lifted to stand clear of the needs HUD (H11): the bubbles sit just above the hotbar's right half, and a box of the needs HUD that
    /// reaches the same columns and the same height (the stage line, which is centred over the hotbar; the block, where a canvas puts it above the hotbar's left end and is
    /// wide enough to reach the bubbles) pushes them up to stand <see cref="Lift"/> above it. Zero where nothing is in the way.
    /// </summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    /// <param name="needs">Where the needs HUD stands, or <see langword="null"/> when it is not drawn.</param>
    public static int RaiseAbove(int canvasWidth, int canvasHeight, NeedsHudBoxes? needs)
    {
        if (needs is null)
        {
            return 0;
        }

        var row = Bubbles(canvasWidth, canvasHeight);
        var left = row[0].X;
        var right = row[^1].Right;
        var top = row[0].Y;
        var bottom = row[0].Bottom;
        var raise = 0;
        foreach (var box in new[] { needs.Block, needs.Line })
        {
            if (box is not { } b || b.Right <= left || b.X >= right || b.Bottom <= top || b.Y >= bottom)
            {
                continue;
            }

            raise = Math.Max(raise, bottom - b.Y + Lift);
        }

        return raise;
    }

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
