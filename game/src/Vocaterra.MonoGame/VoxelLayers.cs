using System.Globalization;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame;

/// <summary>The crosshair at the middle of the screen, and a bar under it that fills while a block is being gathered.</summary>
public sealed class CrosshairLayer : IFrameLayer
{
    private static readonly Color Light = new(255, 255, 255, 235);
    private static readonly Color Outline = new(10, 10, 20, 220);
    private static readonly Color Gold = new(255, 214, 64);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Voxel is not { } sim)
        {
            return;
        }

        var cx = view.ViewportWidth / 2;
        var cy = view.ViewportHeight / 2;
        const int Arm = 9;
        const int Thick = 2;
        var pixel = view.Pixel;
        spriteBatch.Draw(pixel, new Rectangle(cx - Arm - 1, cy - (Thick / 2) - 1, (Arm * 2) + 2, Thick + 2), Outline);
        spriteBatch.Draw(pixel, new Rectangle(cx - (Thick / 2) - 1, cy - Arm - 1, Thick + 2, (Arm * 2) + 2), Outline);
        spriteBatch.Draw(pixel, new Rectangle(cx - Arm, cy - (Thick / 2), Arm * 2, Thick), Light);
        spriteBatch.Draw(pixel, new Rectangle(cx - (Thick / 2), cy - Arm, Thick, Arm * 2), Light);

        if (sim.GatherTarget is { } target && sim.GatherProgress > 0)
        {
            var needed = Math.Max(1, BlockDefinition.Get(sim.Grid.GetBlock(target.X, target.Y, target.Z)).GatherTicks);
            var fraction = Math.Clamp(sim.GatherProgress / (double)needed, 0.0, 1.0);
            var bar = new Rectangle(cx - 30, cy + 26, 60, 8);
            spriteBatch.Draw(pixel, bar, new Color(0, 0, 0, 170));
            spriteBatch.Draw(pixel, new Rectangle(bar.X + 1, bar.Y + 1, (int)((bar.Width - 2) * fraction), bar.Height - 2), Gold);
            DrawHelpers.Border(spriteBatch, pixel, bar, 1, Outline);
        }
    }
}

/// <summary>
/// The held item at the bottom right: an arm in a sleeve and, in the hand, the item of the selected hotbar slot (a piece of wood, a berry, a torch).
/// It sways a little while the player walks and swings while a block is being gathered.
/// </summary>
public sealed class HeldItemLayer : IFrameLayer
{
    private static readonly Color Sleeve = new(52, 168, 92);
    private static readonly Color Skin = new(244, 200, 154);
    private static readonly Color Shadow = new(20, 40, 30);
    private static readonly Color SleeveShade = new(36, 124, 68);
    private static readonly Color Cuff = new(232, 196, 60);
    private static readonly Color SkinShade = new(214, 164, 118);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Voxel is not { } sim)
        {
            return;
        }

        var width = view.ViewportWidth;
        var height = view.ViewportHeight;
        var tick = sim.Tick;
        var moving = sim.Player.VelocityX != 0 || sim.Player.VelocityZ != 0;
        var sway = moving ? new Vector2(MathF.Sin(tick * 0.17f) * 5f, MathF.Abs(MathF.Cos(tick * 0.17f)) * 6f) : Vector2.Zero;
        var swing = sim.GatherProgress > 0 ? MathF.Sin(sim.GatherProgress * 0.55f) : 0f;
        var hand = new Vector2(width - 250 + (swing * 34f), height - 170 + (swing * 22f)) + sway;
        var elbow = new Vector2(width - 40, height + 40);
        var toHand = hand - elbow;
        var angle = MathF.Atan2(toHand.Y, toHand.X);
        var length = toHand.Length();
        var pixel = view.Pixel;
        var along = Vector2.Normalize(toHand);
        var cuffAt = elbow + (along * length * 0.6f);
        // Back to front: a dark outline of the whole arm, the sleeve with a darker lower edge, a cuff, and the hand with a lighter top edge.
        DrawHelpers.Bar(spriteBatch, pixel, elbow, angle, length + 6f, 100f, Shadow);
        DrawHelpers.Bar(spriteBatch, pixel, elbow, angle, length * 0.6f, 92f, SleeveShade);
        DrawHelpers.Bar(spriteBatch, pixel, elbow + (new Vector2(-along.Y, along.X) * -8f), angle, length * 0.6f, 74f, Sleeve);
        DrawHelpers.Bar(spriteBatch, pixel, cuffAt, angle, 16f, 92f, Cuff);
        DrawHelpers.Bar(spriteBatch, pixel, cuffAt + (along * 16f), angle, (length * 0.4f) - 16f, 84f, SkinShade);
        DrawHelpers.Bar(spriteBatch, pixel, cuffAt + (along * 16f) + (new Vector2(-along.Y, along.X) * -8f), angle, (length * 0.4f) - 16f, 62f, Skin);

        var stack = sim.SelectedStack;
        if (!stack.IsEmpty)
        {
            var icon = new Rectangle((int)hand.X - 72, (int)hand.Y - 118, 112, 112);
            view.DrawIcon(spriteBatch, stack.Item, icon);
        }
    }
}

/// <summary>
/// The names over the NPC figures: the Spanish word on a dark plate, at the point above the head, drawn through the font after the scene. A name
/// is hidden behind the camera, farther than <see cref="FullDistance"/> plus the fade, or when a solid block is in the way.
/// </summary>
public sealed class NpcLabelLayer : IFrameLayer
{
    /// <summary>The distance in blocks up to which a label is fully visible.</summary>
    public const double FullDistance = 24.0;

    /// <summary>The further distance over which the label fades out.</summary>
    public const double FadeDistance = 12.0;

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Voxel is not { } sim || view.Camera3D is not { } camera)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var eye = view.Eye;
        foreach (var figure in view.Figures)
        {
            var (x, y, z) = figure.LabelPosition;
            var point = camera.Screen.Project(x, y, z);
            var distance = Math.Sqrt(Math.Pow(x - eye.X, 2) + Math.Pow(y - eye.Y, 2) + Math.Pow(z - eye.Z, 2));
            if (!point.InFront || distance > FullDistance + FadeDistance || Occluded(sim, eye, (x, y, z), distance))
            {
                continue;
            }

            var alpha = (float)Math.Clamp((FullDistance + FadeDistance - distance) / FadeDistance, 0.0, 1.0);
            var width = DrawHelpers.Width(font, figure.Label);
            var box = new Rectangle((int)(point.X - (width / 2)) - 7, (int)point.Y - 12, (int)width + 14, 24);
            spriteBatch.Draw(view.Pixel, box, new Color(0, 0, 0, 175) * alpha);
            DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, new Color(255, 214, 64) * alpha);
            DrawHelpers.Text(spriteBatch, font, figure.Label, new Vector2(box.X + 7, box.Y + 3), Color.White * alpha);
        }
    }

    /// <summary>Whether a solid block lies between the eye and the point (both in blocks), measured with the simulation's own ray.</summary>
    public static bool Occluded(VoxelSimulation sim, Vector3 eye, (float X, float Y, float Z) target, double distance)
    {
        if (distance < 0.5)
        {
            return false;
        }

        const double U = VoxelPhysics.UnitsPerBlock;
        var dx = (int)Math.Round((target.X - eye.X) / distance * Angles.One);
        var dy = (int)Math.Round((target.Y - eye.Y) / distance * Angles.One);
        var dz = (int)Math.Round((target.Z - eye.Z) / distance * Angles.One);
        var hit = BlockRay.Cast(sim.Grid, (int)(eye.X * U), (int)(eye.Y * U), (int)(eye.Z * U), dx, dy, dz, (long)((distance - 0.3) * U));
        return hit is { } h && BlockDefinition.Get(h.Id).Solid;
    }
}

/// <summary>
/// The pointer's screen half: a label over the beacon column when it is on screen, and an arrow on the screen edge when it is not (the column
/// itself is drawn in the scene). The arrow math is <see cref="EdgeArrow"/>.
/// </summary>
public sealed class BeaconArrowLayer : IFrameLayer
{
    private const int EdgeMargin = 56;
    private const float ArrowLength = 34f;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Outline = new(40, 28, 0);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Beacon is not { Target: { } target } beacon || view.Camera3D is not { } camera)
        {
            return;
        }

        var width = view.ViewportWidth;
        var height = view.ViewportHeight;
        var eye = view.Eye;
        var point = camera.Screen.Project(target.X + 0.5, target.Y + 4.0, target.Z + 0.5);
        var arrow = EdgeArrow.For(point, width, height, EdgeMargin);
        var distance = Math.Sqrt(Math.Pow(target.X + 0.5 - eye.X, 2) + Math.Pow(target.Z + 0.5 - eye.Z, 2));
        var text = string.Create(CultureInfo.InvariantCulture, $"{beacon.Label ?? string.Empty}  {distance:0} bloques").Trim();
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var pixel = view.Pixel;
        if (arrow.OnScreen)
        {
            DrawLabel(spriteBatch, pixel, font, text, new Vector2((float)arrow.X, (float)arrow.Y));
            return;
        }

        var from = new Vector2((float)arrow.X, (float)arrow.Y) - (new Vector2(MathF.Cos((float)arrow.Angle), MathF.Sin((float)arrow.Angle)) * (ArrowLength * 0.5f));
        DrawArrow(spriteBatch, pixel, from, (float)arrow.Angle, ArrowLength);
        var labelAt = new Vector2((float)arrow.X, (float)arrow.Y) + new Vector2(0, arrow.Y > height / 2.0 ? -40 : 28);
        DrawLabel(spriteBatch, pixel, font, text, labelAt);
    }

    private static void DrawLabel(SpriteBatch spriteBatch, Texture2D pixel, FontStashSharp.SpriteFontBase font, string text, Vector2 centre)
    {
        var width = DrawHelpers.Width(font, text);
        var box = new Rectangle((int)(centre.X - (width / 2)) - 6, (int)centre.Y - 11, (int)width + 12, 22);
        spriteBatch.Draw(pixel, box, new Color(0, 0, 0, 190));
        DrawHelpers.Border(spriteBatch, pixel, box, 1, Gold);
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(box.X + 6, box.Y + 2), Color.White);
    }

    private static void DrawArrow(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, float angle, float length)
    {
        var tip = from + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length);
        const float Head = 14f;
        foreach (var (thickness, tint) in new[] { (10f, Outline), (6f, Gold) })
        {
            DrawHelpers.Bar(spriteBatch, pixel, from, angle, length, thickness, tint);
            DrawHelpers.Bar(spriteBatch, pixel, tip, angle + (MathF.PI * 0.8f), Head, thickness, tint);
            DrawHelpers.Bar(spriteBatch, pixel, tip, angle - (MathF.PI * 0.8f), Head, thickness, tint);
        }
    }
}

/// <summary>The 3D debug overlay: frames per second, the tick, the position, the facing, and the block under the crosshair (F3 shows or hides it).</summary>
public sealed class VoxelDebugOverlay : IFrameLayer
{
    private const int Margin = 8;
    private const int LineHeight = 20;

    /// <summary>Whether the overlay is shown.</summary>
    public bool Visible { get; set; } = true;

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!Visible || view.Voxel is not { } sim)
        {
            return;
        }

        var lines = VoxelDebugText.Lines(view.Fps, sim, view.Seed, view.Look.Yaw, view.Look.Pitch);
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var width = 0;
        foreach (var line in lines)
        {
            width = Math.Max(width, (int)font.MeasureString(line).X);
        }

        var panel = new Rectangle(Margin / 2, Margin / 2, width + Margin, (lines.Count * LineHeight) + Margin);
        spriteBatch.Draw(view.Pixel, panel, new Color(0, 0, 0, 150));
        for (var i = 0; i < lines.Count; i++)
        {
            spriteBatch.DrawString(font, lines[i], new Vector2(Margin, Margin + (i * LineHeight)), Color.White);
        }
    }
}
