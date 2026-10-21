using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;

namespace Vocaterra.MonoGame;

/// <summary>
/// The arrow the guide puts out when she points ("el árbol está a la derecha"): a pulsing frame and a bouncing marker over the target tile
/// when it is on screen, an arrow at the edge of the screen when it is not, and always an arrow beside the player that turns toward it. It
/// stays until the player reaches the tile (the session clears it) or presses Esc with the dialogue closed.
/// </summary>
public sealed class PointerLayer : IFrameLayer
{
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Outline = new(40, 28, 0);
    private const int EdgeMargin = 40;
    private const float ArrowLength = 34f;

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Session.Pointer is not { } pointer)
        {
            return;
        }

        var camera = view.Camera;
        var size = camera.PixelsPerTile;
        var (tileLeft, tileTop) = camera.TileToScreen(pointer.Target.X, pointer.Target.Y);
        var target = new Vector2(tileLeft + (size / 2f), tileTop + (size / 2f));
        var viewport = new Rectangle(0, 0, camera.ViewportWidth, camera.ViewportHeight);
        var pixel = view.Pixel;
        var tick = view.Simulation!.Tick;
        var font = view.Fonts.Get(FontProvider.NativeSize);

        var playerBox = view.PlayerUnits;
        var (px, py) = view.Camera.UnitsToScreen(playerBox.Center.X, playerBox.Top);
        var player = new Vector2(px, py);
        var toTarget = target - new Vector2(px, py + (size * 1.4f));
        var angle = MathF.Atan2(toTarget.Y, toTarget.X);

        // The arrow beside the player: a short arrow above the head that turns toward the target.
        DrawArrow(spriteBatch, pixel, player + new Vector2(0, -(size * 0.9f)), angle, ArrowLength, Gold);

        var inner = new Rectangle(EdgeMargin, EdgeMargin, Math.Max(1, viewport.Width - (EdgeMargin * 2)), Math.Max(1, viewport.Height - (EdgeMargin * 2)));
        if (inner.Contains((int)target.X, (int)target.Y))
        {
            // The target is on screen: a frame that pulses and a marker that bounces over it.
            var pulse = (int)((tick / 8) % 4);
            var frame = new Rectangle(tileLeft - pulse, tileTop - pulse, size + (pulse * 2), size + (pulse * 2));
            DrawHelpers.Border(spriteBatch, pixel, frame, 2, Gold);
            var bounce = (int)((tick / 10) % 2) * 4;
            DrawMarker(spriteBatch, pixel, new Vector2(target.X, tileTop - 18 - bounce));
            DrawLabel(spriteBatch, pixel, font, LabelOf(pointer), new Vector2(target.X, tileTop - 56 - bounce));
        }
        else
        {
            // Off screen: an arrow at the edge, where the line from the screen centre to the target leaves the screen.
            var center = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
            var direction = target - center;
            var scale = MathF.Min(
                direction.X == 0 ? float.MaxValue : MathF.Abs((inner.Width / 2f) / direction.X),
                direction.Y == 0 ? float.MaxValue : MathF.Abs((inner.Height / 2f) / direction.Y));
            var edge = center + (direction * scale);
            DrawArrow(spriteBatch, pixel, edge - (Vector2.Normalize(direction) * (ArrowLength * 0.5f)), MathF.Atan2(direction.Y, direction.X), ArrowLength, Gold);
            DrawLabel(spriteBatch, pixel, font, LabelOf(pointer), edge + new Vector2(0, -34));
        }

        if (!view.Dialogue.IsOpen)
        {
            const string Dismiss = "Esc: quitar la flecha";
            var width = DrawHelpers.Width(font, Dismiss);
            DrawHelpers.Text(spriteBatch, font, Dismiss, new Vector2(viewport.Width - width - 12, 12), new Color(255, 236, 160));
        }
    }

    /// <summary>The label of the marker over the target: the noun, or <c>¿?</c> for the review question (the noun is the answer).</summary>
    public const string QuizLabel = "¿?";

    private static string LabelOf(PointerMarker pointer) => pointer.Quiz ? QuizLabel : pointer.Place;

    private static void DrawLabel(SpriteBatch spriteBatch, Texture2D pixel, FontStashSharp.SpriteFontBase font, string label, Vector2 center)
    {
        var width = DrawHelpers.Width(font, label);
        var box = new Rectangle((int)(center.X - (width / 2)) - 5, (int)center.Y - 2, (int)width + 10, 20);
        spriteBatch.Draw(pixel, box, new Color(0, 0, 0, 190));
        DrawHelpers.Border(spriteBatch, pixel, box, 1, Gold);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(box.Left + 5, box.Top + 1), Color.White);
    }

    private static void DrawArrow(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, float angle, float length, Color color)
    {
        var tip = from + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length);
        const float Head = 12f;
        foreach (var (thickness, tint) in new[] { (9f, Outline), (5f, color) })
        {
            DrawHelpers.Bar(spriteBatch, pixel, from, angle, length, thickness, tint);
            DrawHelpers.Bar(spriteBatch, pixel, tip, angle + (MathF.PI * 0.8f), Head, thickness, tint);
            DrawHelpers.Bar(spriteBatch, pixel, tip, angle - (MathF.PI * 0.8f), Head, thickness, tint);
        }
    }

    private static void DrawMarker(SpriteBatch spriteBatch, Texture2D pixel, Vector2 tip)
    {
        // A downward-pointing triangle built from rows, with a dark outline.
        const int Height = 14;
        for (var row = 0; row < Height; row++)
        {
            var half = Height - row;
            var y = (int)tip.Y - Height + row;
            spriteBatch.Draw(pixel, new Rectangle((int)tip.X - half - 2, y - 1, (half * 2) + 4, 3), Outline);
        }

        for (var row = 0; row < Height; row++)
        {
            var half = Height - row;
            var y = (int)tip.Y - Height + row;
            spriteBatch.Draw(pixel, new Rectangle((int)tip.X - half, y, half * 2, 1), Gold);
        }
    }
}
