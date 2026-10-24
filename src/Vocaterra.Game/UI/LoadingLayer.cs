using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Graphics;

namespace Vocaterra.Game.UI;

/// <summary>
/// The loading screen: a dark veil over the world with a line of text while the turn worker loads the pipeline and the speech models
/// (<see cref="FrameView.Loading"/> is set). The world is not interactive behind it: the game neither steps the simulation nor reads keys
/// until the worker is ready, so the first utterance a player waits for does not pay for a model load.
/// </summary>
public sealed class LoadingLayer : IFrameLayer
{
    /// <summary>The line shown while the models load.</summary>
    public const string Text = "Loading speech…";

    private static readonly Color Gold = new(255, 214, 64);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Loading is not { } text)
        {
            return;
        }

        spriteBatch.Draw(view.Pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), new Color(8, 8, 20, 235));
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var width = DrawHelpers.Width(font, text);
        var box = new Rectangle((int)((view.ViewportWidth - width) / 2) - 24, (view.ViewportHeight / 2) - 24, (int)width + 48, 48);
        spriteBatch.Draw(view.Pixel, box, new Color(0, 0, 0, 220));
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold);
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(box.Left + 24, box.Top + 14), Color.White);

        // A bar that runs along the box so a stalled load can be told from a running one (the tick does not advance while loading).
        var run = (int)((box.Width - 8) * (view.LoadingFrame % 60 / 60.0));
        spriteBatch.Draw(view.Pixel, new Rectangle(box.Left + 4, box.Bottom - 8, run, 3), Gold);
    }
}
