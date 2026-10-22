using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The hint under the crosshair when it is on an NPC's figure (S3, every NPC since S6): <c>E: hablar</c>, or <c>T: escribir</c> for the typed panel. It is hidden
/// while the panel is open.
/// </summary>
public sealed class TalkHintLayer : IFrameLayer
{
    /// <summary>The hint's text.</summary>
    public const string Text = "E: hablar";

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Aimed is null || view.Dialogue.IsOpen || view.Voxel is null)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var width = DrawHelpers.Width(font, Text);
        var screen = view.ViewportWidth / 2;
        var box = new Rectangle((int)(screen - (width / 2)) - 8, (view.ViewportHeight / 2) + 26, (int)width + 16, 24);
        spriteBatch.Draw(view.Pixel, box, new Color(0, 0, 0, 175));
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, new Color(255, 214, 64));
        DrawHelpers.Text(spriteBatch, font, Text, new Vector2(box.X + 8, box.Y + 3), Color.White);
    }
}

/// <summary>
/// A short label drawn over a fixed point of the scene (S3: the signpost's <c>Tienda / Río</c>). It is hidden when a solid block is in the way
/// (<see cref="NpcLabelLayer.Occluded"/>), and fades like the NPC labels.
/// </summary>
/// <param name="Text">The label.</param>
/// <param name="X">The point on x, in blocks.</param>
/// <param name="Y">The point on y, in blocks.</param>
/// <param name="Z">The point on z, in blocks.</param>
public sealed record WorldLabel(string Text, float X, float Y, float Z);

/// <summary>Draws the scene's world labels (<see cref="WorldLabel"/>) over their points, when they are in view.</summary>
/// <param name="labels">The labels, drawn in order.</param>
public sealed class WorldLabelLayer(IReadOnlyList<WorldLabel> labels) : IFrameLayer
{
    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Voxel is not { } sim || view.Camera3D is not { } camera)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var eye = view.Eye;
        foreach (var label in labels)
        {
            var point = camera.Screen.Project(label.X, label.Y, label.Z);
            var distance = Math.Sqrt(Math.Pow(label.X - eye.X, 2) + Math.Pow(label.Y - eye.Y, 2) + Math.Pow(label.Z - eye.Z, 2));
            if (!point.InFront || distance > NpcLabelLayer.FullDistance + NpcLabelLayer.FadeDistance
                || NpcLabelLayer.Occluded(sim, eye, (label.X, label.Y, label.Z), distance))
            {
                continue;
            }

            var alpha = (float)Math.Clamp((NpcLabelLayer.FullDistance + NpcLabelLayer.FadeDistance - distance) / NpcLabelLayer.FadeDistance, 0.0, 1.0);
            var width = DrawHelpers.Width(font, label.Text);
            var box = new Rectangle((int)(point.X - (width / 2)) - 7, (int)point.Y - 12, (int)width + 14, 24);
            spriteBatch.Draw(view.Pixel, box, new Color(0, 0, 0, 175) * alpha);
            DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, new Color(255, 214, 64) * alpha);
            DrawHelpers.Text(spriteBatch, font, label.Text, new Vector2(box.X + 7, box.Y + 3), Color.White * alpha);
        }
    }
}
