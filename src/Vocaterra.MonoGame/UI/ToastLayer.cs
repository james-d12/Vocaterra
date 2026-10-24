using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Graphics;

namespace Vocaterra.MonoGame.UI;

/// <summary>
/// The "new word" toast: a line across the top of the screen (<c>¡Nueva palabra: el árbol!</c>) for <see cref="ToastQueue.Duration"/> ticks, fading
/// out over its last half second. The lines come from the game when a noun becomes known.
/// </summary>
public sealed class ToastLayer : IFrameLayer
{
    private const int FadeTicks = 30;

    private static readonly Color Gold = new(255, 214, 64);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var tick = view.Tick;
        if (view.Toasts.Current(tick) is not { } toast)
        {
            return;
        }

        var age = tick - toast.StartTick;
        var fade = Math.Clamp((ToastQueue.Duration - age) / (double)FadeTicks, 0.0, 1.0);
        var alpha = (float)fade;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var (toastBox, rows) = HudLayout.Toast(view.ViewportWidth, toast.Text, text => DrawHelpers.Width(font, text));
        var box = new Rectangle(toastBox.X, toastBox.Y, toastBox.Width, toastBox.Height);
        spriteBatch.Draw(view.Pixel, box, new Color(0, 0, 0, 200) * alpha);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold * alpha);
        for (var i = 0; i < rows.Count; i++)
        {
            DrawHelpers.Text(spriteBatch, font, rows[i], new Vector2(box.Left + HudLayout.ToastPadding, box.Top + 7 + (i * HudLayout.RowHeight)), Color.White * alpha);
        }
    }
}
