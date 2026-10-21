using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The "new word" toast: a line across the top of the screen (<c>¡Nueva palabra: el árbol!</c>) for <see cref="ToastQueue.Duration"/> ticks, fading
/// out over its last half second. The lines come from the game when a noun becomes known.
/// </summary>
public sealed class ToastLayer : IFrameLayer
{
    private const int FadeTicks = 30;
    private const int Top = 56;

    private static readonly Color Gold = new(255, 214, 64);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var tick = view.Simulation.Tick;
        if (view.Toasts.Current(tick) is not { } toast)
        {
            return;
        }

        var age = tick - toast.StartTick;
        var fade = Math.Clamp((ToastQueue.Duration - age) / (double)FadeTicks, 0.0, 1.0);
        var alpha = (float)fade;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var width = DrawHelpers.Width(font, toast.Text);
        var viewportWidth = view.Camera.ViewportWidth;
        var box = new Rectangle((int)((viewportWidth - width) / 2) - 14, Top, (int)width + 28, 30);
        spriteBatch.Draw(view.Pixel, box, new Color(0, 0, 0, 200) * alpha);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold * alpha);
        DrawHelpers.Text(spriteBatch, font, toast.Text, new Vector2(box.Left + 14, box.Top + 7), Color.White * alpha);
    }
}
