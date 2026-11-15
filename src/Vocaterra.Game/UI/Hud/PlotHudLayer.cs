using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// The plot's line (Y10): <c>Plot 10 × 10 · price 10 coins</c> for the marked plot, <c>Your land</c> for the owned one, under the crosshair in the hint style, a little lower than the talk hint
/// so the two never cover each other. Numbers and words only, never a colour alone. The session decides when there is a line (<c>VoxelSession.PlotHudLine</c>); a talk hides it.
/// </summary>
public sealed class PlotHudLayer : IFrameLayer
{
    /// <summary>How far under the talk hint's row the line stands, in pixels.</summary>
    public const int Drop = 30;

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.PlotLine is not { } text || view.Dialogue.IsOpen)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var width = DrawHelpers.Width(font, text);
        var box = new Rectangle((int)((view.ViewportWidth / 2) - (width / 2)) - 8, (view.ViewportHeight / 2) + 26 + Drop, (int)width + 16, 24);
        spriteBatch.Draw(view.Pixel, box, new Color(0, 0, 0, 175));
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, new Color(255, 214, 64));
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(box.X + 8, box.Y + 3), Color.White);
    }
}
