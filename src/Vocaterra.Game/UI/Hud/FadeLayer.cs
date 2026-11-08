using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Hud;

/// <summary>The fade of a collapse (N03): the world goes black as the player falls asleep and clears again on the morning. Drawn first, so the interface stays on top.</summary>
public sealed class FadeLayer : IFrameLayer
{
    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.FadeAlpha <= 0f)
        {
            return;
        }

        spriteBatch.Draw(view.Pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), Color.Black * Math.Min(1f, view.FadeAlpha));
    }
}
