using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// The needs on the HUD (N05, design 9.9): a thin health bar and, while stamina is below its maximum, a thin stamina bar with a dashed end for what the drives cut, a row of
/// drive icons (a loaf, a drop, a moon) with a notch for each stage, and a stage line above the hotbar (the Spanish with the English beneath) for a few seconds when a stage
/// gets worse. With healthy drives and full stamina only the health bar shows. Where everything stands is <see cref="NeedsHudLayout"/>; this layer only draws.
/// </summary>
public sealed class NeedsHudLayer : IFrameLayer
{
    private const int FadeTicks = 30;

    private static readonly Color Back = new(18, 20, 28, 210);
    private static readonly Color Frame = new(88, 92, 108);
    private static readonly Color HealthFill = new(214, 72, 72);
    private static readonly Color HealthPulse = new(255, 130, 120);
    private static readonly Color StaminaFill = new(120, 200, 110);
    private static readonly Color Dashed = new(120, 124, 140);
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color LineBack = new(10, 10, 20, 215);

    /// <summary>Where the needs HUD stands for this frame (the bubble meter stacks above it, H11).</summary>
    /// <param name="view">The frame.</param>
    /// <param name="hud">The needs HUD state.</param>
    /// <param name="font">The small font the stage line is measured in.</param>
    internal static NeedsHudBoxes PlaceFor(FrameView view, Core.Needs.NeedsHud hud, FontStashSharp.SpriteFontBase font)
    {
        var announcement = hud.Announcement;
        return NeedsHudLayout.Place(view.ViewportWidth, view.ViewportHeight, hud.Icons.Count, hud.StaminaVisible, announcement?.Spanish, announcement?.English, text => DrawHelpers.Width(font, text));
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Needs is not { Visible: true } hud || view.Check is not null || view.MicCheck is { IsActive: true })
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var pixel = view.Pixel;
        var icons = hud.Icons;
        var announcement = hud.Announcement;
        var boxes = PlaceFor(view, hud, font);

        for (var i = 0; i < icons.Count; i++)
        {
            DrawIcon(spriteBatch, pixel, Rect(boxes.Icons[i]), icons[i]);
        }

        var pulse = (float)hud.HeartPulse(view.Tick);
        DrawBar(spriteBatch, pixel, Rect(boxes.Health), hud.Health, 1.0, Color.Lerp(HealthFill, HealthPulse, pulse), hud.HealthStage > 0);
        if (boxes.Stamina is { } staminaBox)
        {
            DrawBar(spriteBatch, pixel, Rect(staminaBox), hud.Stamina, hud.StaminaMaximum, StaminaFill, false);
        }

        if (announcement is not null && boxes.Line is { } lineBox)
        {
            var age = view.Tick - announcement.StartTick;
            var alpha = (float)Math.Clamp((NeedsHud.DisplayTicks - age) / (double)FadeTicks, 0.0, 1.0);
            var box = Rect(lineBox);
            spriteBatch.Draw(pixel, box, LineBack * alpha);
            DrawHelpers.Border(spriteBatch, pixel, box, 1, Gold * alpha);
            DrawHelpers.Text(spriteBatch, font, announcement.Spanish, new Vector2(box.X + NeedsHudLayout.LinePadding, box.Y + 6), Ink * alpha);
            DrawHelpers.Text(spriteBatch, font, announcement.English, new Vector2(box.X + NeedsHudLayout.LinePadding, box.Y + 6 + HudLayout.RowHeight), DialoguePanel.EnglishColor * alpha);
        }
    }

    private static Rectangle Rect(PixelBox box) => new(box.X, box.Y, box.Width, box.Height);

    // A thin bar: the fill up to the value, and from the maximum to the end a dashed stretch for what the drives cut. A glow at the bar's edge when it pulses.
    private static void DrawBar(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, double value, double maximum, Color fill, bool warn)
    {
        spriteBatch.Draw(pixel, box, Back);
        var filled = (int)Math.Round((box.Width - 2) * Math.Clamp(value, 0, 1));
        if (filled > 0)
        {
            spriteBatch.Draw(pixel, new Rectangle(box.X + 1, box.Y + 1, filled, box.Height - 2), fill);
        }

        var cutStart = box.X + 1 + (int)Math.Round((box.Width - 2) * Math.Clamp(maximum, 0, 1));
        for (var x = cutStart; x < box.Right - 1; x += 6)
        {
            spriteBatch.Draw(pixel, new Rectangle(x, box.Y + 2, Math.Min(3, box.Right - 1 - x), box.Height - 4), Dashed);
        }

        DrawHelpers.Border(spriteBatch, pixel, box, 1, warn ? HealthPulse : Frame);
    }

    // The icon: its shape in the drive's colour, then four notches under it, one filled for each stage.
    private static void DrawIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle cell, DriveIcon icon)
    {
        var colour = icon.Drive switch
        {
            Drive.Hunger => new Color(214, 160, 90),
            Drive.Thirst => new Color(96, 176, 240),
            _ => new Color(190, 190, 255),
        };
        var left = cell.X + 2;
        foreach (var (y, x0, x1) in NeedIcons.Shape(icon.Drive))
        {
            spriteBatch.Draw(pixel, new Rectangle(left + x0, cell.Y + y, x1 - x0 + 1, 1), colour);
        }

        foreach (var (x, y, width, height) in NeedIcons.Marks(icon.Drive))
        {
            spriteBatch.Draw(pixel, new Rectangle(left + x, cell.Y + y, width, height), new Color(150, 100, 50));
        }

        var notch = icon.Notches switch { <= 2 => new Color(240, 220, 130), 3 => new Color(255, 150, 60), _ => new Color(240, 70, 60) };
        for (var n = 0; n < 4; n++)
        {
            var box = new Rectangle(cell.X + 1 + (n * 5), cell.Y + NeedIcons.Size + 4, 4, 3);
            spriteBatch.Draw(pixel, box, n < icon.Notches ? notch : Dashed * 0.6f);
        }
    }
}
