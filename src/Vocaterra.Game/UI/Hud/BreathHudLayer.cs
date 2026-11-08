using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// The bubble meter (H04): ten bubbles above the right half of the hotbar, one lost with each tenth of the air, shown under water and while the air is
/// below full, as <see cref="BreathMeter"/> decides. The layer only draws; the state lives in the meter.
/// </summary>
public sealed class BreathHudLayer : IFrameLayer
{
    private readonly BreathMeter _meter = new();

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var frame = _meter.Update(view.Breath, view.Tick);
        if (frame.Alpha <= 0f)
        {
            return;
        }

        NeedsHudBoxes? needs = null;
        if (view.Needs is { Visible: true } hud && view.Check is null && view.MicCheck is not { IsActive: true })
        {
            needs = NeedsHudLayer.PlaceFor(view, hud, view.Fonts.Get(FontProvider.NativeSize));
        }

        var boxes = BreathHudLayout.Bubbles(view.ViewportWidth, view.ViewportHeight, BreathHudLayout.RaiseAbove(view.ViewportWidth, view.ViewportHeight, needs));
        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            var area = new Rectangle(box.X, box.Y, box.Width, box.Height);
            var size = frame.Sizes[i];
            if (size > 0f)
            {
                BubbleIcon.Draw(spriteBatch, view.Pixel, area, size, frame.Alpha);
            }
            else
            {
                BubbleIcon.DrawEmpty(spriteBatch, view.Pixel, area, frame.Alpha);
            }
        }
    }
}
