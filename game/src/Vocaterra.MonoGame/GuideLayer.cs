using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.Simulation.Physics;

namespace Vocaterra.MonoGame;

/// <summary>
/// The guide as a placeholder figure (a robed body, a head, a pointed hat and eyes that look at the player) standing on the tile
/// <see cref="GuideNpc.Feet"/>, with her name over her head and, when the player is close enough and the dialogue is closed, the hint
/// "Pulsa T para hablar". Real art replaces the rectangles later (D-game-5: CC0 only).
/// </summary>
public sealed class GuideLayer : IFrameLayer
{
    /// <summary>The hint shown when the player is near the guide.</summary>
    public const string Hint = "Pulsa T para hablar";

    /// <summary>The guide's name over her head.</summary>
    public const string Name = "Guía";

    private static readonly Color Robe = new(112, 72, 170);
    private static readonly Color RobeDark = new(80, 48, 130);
    private static readonly Color Skin = new(240, 204, 164);
    private static readonly Color Hat = new(40, 160, 150);
    private static readonly Color HatBand = new(240, 200, 60);
    private static readonly Color Eye = new(30, 30, 40);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var camera = view.Camera;
        var size = camera.PixelsPerTile;
        var feet = view.Slice!.Guide.Feet;
        var (left, top) = camera.TileToScreen(feet.X, feet.Y);
        var centerX = left + (size / 2);
        var bottom = top + size;
        if (centerX < -size * 4 || centerX > camera.ViewportWidth + (size * 4))
        {
            return;
        }

        var width = size * 5 / 4;
        var height = size * 11 / 4;
        var body = new Rectangle(centerX - (width / 2), bottom - (height * 6 / 11), width, height * 6 / 11);
        var faceSize = width * 3 / 5;
        var head = new Rectangle(centerX - (faceSize / 2), body.Top - faceSize + 2, faceSize, faceSize);
        var pixel = view.Pixel;

        spriteBatch.Draw(pixel, body, Robe);
        spriteBatch.Draw(pixel, new Rectangle(body.Left, body.Bottom - 4, body.Width, 4), RobeDark);
        spriteBatch.Draw(pixel, new Rectangle(body.Left + (width / 2) - 2, body.Top, 4, body.Height), RobeDark);
        spriteBatch.Draw(pixel, head, Skin);

        // The hat: a band, a narrow crown and a tip, so the figure reads as someone else from a distance.
        spriteBatch.Draw(pixel, new Rectangle(head.Left - 4, head.Top - 4, head.Width + 8, 6), Hat);
        spriteBatch.Draw(pixel, new Rectangle(head.Left - 4, head.Top - 2, head.Width + 8, 2), HatBand);
        spriteBatch.Draw(pixel, new Rectangle(head.Left + 2, head.Top - 14, head.Width - 4, 11), Hat);
        spriteBatch.Draw(pixel, new Rectangle(head.Left + (head.Width / 2) - 3, head.Top - 22, 6, 9), Hat);

        // The eyes look at the player.
        var look = view.PlayerUnits.Center.X < (feet.X * PhysicsConstants.UnitsPerTile) + (PhysicsConstants.UnitsPerTile / 2) ? -1 : 1;
        var eye = Math.Max(3, faceSize / 6);
        var eyeY = head.Top + (faceSize / 3);
        var shift = look * 2;
        spriteBatch.Draw(pixel, new Rectangle(head.Left + (faceSize / 4) - (eye / 2) + shift, eyeY, eye, eye), Eye);
        spriteBatch.Draw(pixel, new Rectangle(head.Right - (faceSize / 4) - (eye / 2) + shift, eyeY, eye, eye), Eye);

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var nameY = head.Top - 22 - 22;
        DrawHelpers.Text(spriteBatch, font, Name, new Vector2(centerX - (DrawHelpers.Width(font, Name) / 2), nameY), Color.White);

        if (view.Slice!.IsNearGuide && !view.Dialogue.IsOpen)
        {
            var textWidth = DrawHelpers.Width(font, Hint);
            var box = new Rectangle((int)(centerX - (textWidth / 2)) - 8, nameY - 30, (int)textWidth + 16, 24);
            spriteBatch.Draw(pixel, box, new Color(0, 0, 0, 190));
            DrawHelpers.Border(spriteBatch, pixel, box, 1, HatBand);
            DrawHelpers.Text(spriteBatch, font, Hint, new Vector2(box.Left + 8, box.Top + 3), Color.White);
        }
    }
}
