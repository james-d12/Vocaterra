using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Camera;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;
using DrawHelpers = Vocaterra.Game.Graphics.DrawHelpers;

namespace Vocaterra.Game.UI.Hud;

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
            var block = sim.Grid.GetBlock(target.X, target.Y, target.Z);
            var needed = block == BlockId.Water ? VoxelSimulation.BottleFillTicks : Math.Max(1, sim.GatherTicksOf(block)); // the fill of a bottle builds the same bar; the axe shortens a trunk's (V03)
            var fraction = Math.Clamp(sim.GatherProgress / (double)needed, 0.0, 1.0);
            var bar = new Rectangle(cx - 30, cy + 26, 60, 8);
            spriteBatch.Draw(pixel, bar, new Color(0, 0, 0, 170));
            spriteBatch.Draw(pixel, new Rectangle(bar.X + 1, bar.Y + 1, (int)((bar.Width - 2) * fraction), bar.Height - 2), Gold);
            DrawHelpers.Border(spriteBatch, pixel, bar, 1, Outline);
        }
    }
}

/// <summary>
/// The names over the NPC figures: the Spanish word on a dark plate, at the point above the head, drawn through the font in the world's pass, after the figures and
/// before the first-person arm (V22). Only the plates <see cref="NamePlates.Select"/> picks are drawn, and each is depth-tested against the scene and the figures (it
/// reads the depth buffer and does not write it), so a wall, a building or another figure in front hides the plate, or the part of it behind them. A name is also
/// hidden behind the camera and farther than <see cref="FullDistance"/> plus the fade.
/// </summary>
public static class NpcLabelLayer
{
    /// <summary>The distance in blocks up to which a label is fully visible.</summary>
    public const double FullDistance = 24.0;

    /// <summary>The further distance over which the label fades out.</summary>
    public const double FadeDistance = 12.0;

    /// <summary>How far, in blocks, the plate's depth is moved towards the eye from the point above the head, so the plate's lower half is not hidden by its own figure's head and hat.</summary>
    public const float DepthPull = 0.7f;

    /// <summary>
    /// Where a name's plate stands for the point above the head projected at <paramref name="x"/>, <paramref name="y"/> on the canvas, or <see langword="null"/>
    /// when the plate would lie wholly outside the canvas. A plate partly over an edge is moved inside it (X08); one whose figure is out of the picture, beside
    /// the player but in front of the camera, is left out (VILLAGE_PLAN V09: the farmer's name stood at the window's left edge while the player talked to the
    /// villager beside him, the plate of a figure nobody could see).
    /// </summary>
    /// <param name="x">The projected x of the point above the head.</param>
    /// <param name="y">The projected y.</param>
    /// <param name="textWidth">The width of the name's text.</param>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    public static PixelBox? Place(double x, double y, double textWidth, int viewportWidth, int viewportHeight)
    {
        var plate = new PixelBox((int)(x - (textWidth / 2)) - 7, (int)y - 12, (int)textWidth + 14, 24);
        var outside = plate.X + plate.Width <= 0 || plate.X >= viewportWidth || plate.Y + plate.Height <= 0 || plate.Y >= viewportHeight;
        return outside ? null : Called.Clamp(plate, viewportWidth, viewportHeight);
    }

    /// <summary>
    /// Draws the plates of the figures picked for this frame, over the world drawn so far: each in its own sprite batch under the interface transform, moved in depth to the
    /// plate's place in the scene (a sprite's z is its depth-buffer value under the batch's projection).
    /// </summary>
    /// <param name="device">The graphics device, whose depth buffer holds the scene and the figures.</param>
    /// <param name="spriteBatch">The sprite batch, not begun.</param>
    /// <param name="view">The frame's view.</param>
    /// <param name="camera">The camera of this frame.</param>
    public static void Draw(GraphicsDevice device, SpriteBatch spriteBatch, FrameView view, FirstPersonCamera camera)
    {
        var shown = NamePlates.Select(view.Figures, view.Aimed, view.Beacon?.Target, view.Dialogue.IsOpen ? view.Dialogue.Npc : null);
        if (shown.Count == 0)
        {
            return;
        }

        var parameters = device.PresentationParameters;
        UiTransform.Set(parameters.BackBufferWidth, parameters.BackBufferHeight, view.ViewportWidth, view.ViewportHeight);
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var eye = view.Eye;
        var viewProjection = camera.View * camera.Projection;
        foreach (var figure in shown)
        {
            var (x, y, z) = figure.LabelPosition;
            var point = camera.Screen.Project(x, y, z);
            var distance = Math.Sqrt(Math.Pow(x - eye.X, 2) + Math.Pow(y - eye.Y, 2) + Math.Pow(z - eye.Z, 2));
            if (!point.InFront || distance > FullDistance + FadeDistance)
            {
                continue;
            }

            var alpha = (float)Math.Clamp((FullDistance + FadeDistance - distance) / FadeDistance, 0.0, 1.0);
            var width = DrawHelpers.Width(font, figure.Label);
            if (Place(point.X, point.Y, width, view.ViewportWidth, view.ViewportHeight) is not { } placed)
            {
                continue;
            }

            var pulled = new Vector3(x, y, z) + (Vector3.Normalize(eye - new Vector3(x, y, z)) * DepthPull);
            var clip = Vector4.Transform(new Vector4(pulled, 1f), viewProjection);
            var depth = Math.Clamp(clip.Z / clip.W, 0f, 1f);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.DepthRead, RasterizerState.CullNone, null, UiTransform.Matrix * Matrix.CreateTranslation(0f, 0f, depth));
            var box = new Rectangle(placed.X, placed.Y, placed.Width, placed.Height);
            spriteBatch.Draw(view.Pixel, box, new Color(0, 0, 0, 175) * alpha);
            DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, new Color(255, 214, 64) * alpha);
            DrawHelpers.Outlined(spriteBatch, font, figure.Label, new Vector2(box.X + 7, box.Y + 3), Color.White * alpha);
            spriteBatch.End();
        }
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

    /// <summary>
    /// The beacon's label (S17b): the noun's Spanish label (<c>campamento</c>, a taught noun, so it stays Spanish) and the distance in the game's English unit,
    /// for example <c>campamento 74 blocks</c>.
    /// </summary>
    /// <param name="ui">The support language (<c>ui.beaconDistance</c>, C04).</param>
    /// <param name="label">The beacon's label, or <see langword="null"/>.</param>
    /// <param name="distance">The distance in blocks, on the ground.</param>
    public static string BeaconText(LocaleText ui, string? label, double distance)
    {
        ArgumentNullException.ThrowIfNull(ui);
        var blocks = (int)Math.Round(distance, MidpointRounding.AwayFromZero); // as the "0" format rounded it
        return ui.Plural("ui.beaconDistance", blocks, ("label", label ?? string.Empty)).Trim();
    }

    /// <summary>
    /// The beacon's arrow, label text and label box for a frame (S22), or <see langword="null"/> when the beacon has no target. The box is <see langword="null"/> while the
    /// target is on the screen (the label then hangs at the target); otherwise it is clamped inside the window and clear of <paramref name="avoid"/>.
    /// </summary>
    /// <param name="view">The frame's view.</param>
    /// <param name="avoid">Rectangles the label never overlaps (the panel and the lesson strip while a talk is open).</param>
    /// <param name="soft">Rectangles the label keeps clear of when there is room (the head and the bubble), or <see langword="null"/> to treat <paramref name="avoid"/> alone, as before.</param>
    internal static (EdgeArrow Arrow, string Text, PixelBox? Box)? LabelFor(FrameView view, IReadOnlyList<PixelBox> avoid, IReadOnlyList<PixelBox>? soft = null)
    {
        if (view.Beacon is not { Target: { } target } beacon || view.Camera3D is not { } camera)
        {
            return null;
        }

        var width = view.ViewportWidth;
        var height = view.ViewportHeight;
        var eye = view.Eye;
        var point = camera.Screen.Project(target.X + 0.5, target.Y + 4.0, target.Z + 0.5);
        var arrow = EdgeArrow.For(point, width, height, EdgeMargin);
        var distance = Math.Sqrt(Math.Pow(target.X + 0.5 - eye.X, 2) + Math.Pow(target.Z + 0.5 - eye.Z, 2));
        var text = BeaconText(view.Session.Locale, beacon.Label, distance);
        if (arrow.OnScreen)
        {
            return (arrow, text, null);
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        return (arrow, text, soft is null ? Called.Place(width, height, arrow.X, arrow.Y, DrawHelpers.Width(font, text), avoid) : Called.TryPlace(width, height, arrow.X, arrow.Y, DrawHelpers.Width(font, text), avoid, soft));
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var avoid = new List<PixelBox>();
        if (view.Dialogue.IsOpen)
        {
            avoid.Add(DialogueLayout.PanelBox(view.ViewportWidth, view.ViewportHeight));
        }

        if (LabelFor(view, avoid) is not { } beacon)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var pixel = view.Pixel;
        var (arrow, text, _) = beacon;
        if (arrow.OnScreen)
        {
            DrawLabel(spriteBatch, pixel, font, text, new Vector2((float)arrow.X, (float)arrow.Y), view.ViewportWidth, view.ViewportHeight);
            return;
        }

        var from = new Vector2((float)arrow.X, (float)arrow.Y) - (new Vector2(MathF.Cos((float)arrow.Angle), MathF.Sin((float)arrow.Angle)) * (ArrowLength * 0.5f));
        DrawArrow(spriteBatch, pixel, from, (float)arrow.Angle, ArrowLength);

        // The label stays inside the window and clear of the conversation's panel, head, bubble and cards (S22): the frame places it first and the cards keep clear of it.
        var box = new Rectangle(0, 0, 0, 0);
        if (view.Dialogue.IsOpen)
        {
            if (ConversationFrame.For(view).Called is not { } placed)
            {
                return; // no free place for the label on this canvas (a tall lesson strip over the panel): the arrow stays, the label is left out (X08)
            }

            box = new Rectangle(placed.X, placed.Y, placed.Width, placed.Height);
        }
        else if (beacon.Box is { } plain)
        {
            box = new Rectangle(plain.X, plain.Y, plain.Width, plain.Height);
        }

        spriteBatch.Draw(pixel, box, new Color(0, 0, 0, 190));
        DrawHelpers.Border(spriteBatch, pixel, box, 1, Gold);
        DrawHelpers.Outlined(spriteBatch, font, text, new Vector2(box.X + Called.Padding, box.Y + 2), Color.White);
    }

    private static void DrawLabel(SpriteBatch spriteBatch, Texture2D pixel, FontStashSharp.SpriteFontBase font, string text, Vector2 centre, int viewportWidth, int viewportHeight)
    {
        var width = DrawHelpers.Width(font, text);
        var placed = Called.Clamp(new PixelBox((int)(centre.X - (width / 2)) - 6, (int)centre.Y - 11, (int)width + 12, 22), viewportWidth, viewportHeight);
        var box = new Rectangle(placed.X, placed.Y, placed.Width, placed.Height);
        spriteBatch.Draw(pixel, box, new Color(0, 0, 0, 190));
        DrawHelpers.Border(spriteBatch, pixel, box, 1, Gold);
        DrawHelpers.Outlined(spriteBatch, font, text, new Vector2(box.X + 6, box.Y + 2), Color.White);
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

    /// <summary>The narrowest canvas the overlay is drawn on (X08): below it the overlay would run under the checklist.</summary>
    public const int MinCanvasWidth = 900;

    /// <summary>Whether the overlay is shown.</summary>
    public bool Visible { get; set; } = true;

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!Visible || view.Voxel is not { } sim)
        {
            return;
        }

        if (view.ViewportWidth < MinCanvasWidth || (view.Dialogue.IsOpen && DialogueLayout.IsCompact(view.ViewportHeight)))
        {
            return; // on a narrow or short canvas the developer's overlay gives way to the interface and the conversation (X08)
        }

        var lines = VoxelDebugText.Lines(view.Fps, sim, view.Seed, view.Look.Yaw, view.Look.Pitch, view.Session.Day);
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
