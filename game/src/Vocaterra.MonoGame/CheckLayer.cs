using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Items;

namespace Vocaterra.MonoGame;

/// <summary>
/// The check battery's screen (S11): a dimmed world behind a panel. A comprehension item shows <c>Listen. What does it mean?</c>, the line plays
/// (the pre-rendered speech), and three options with their icons; a production item shows the English situation and the keys to answer it. It
/// only draws the <see cref="CheckScreen"/>; the keys are the game's.
/// </summary>
public sealed class CheckLayer : IFrameLayer
{
    private const int TitleSize = 32;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Dim = new(150, 150, 165);
    private static readonly Color Good = new(120, 220, 130);
    private static readonly Color Warn = new(255, 150, 120);

    /// <summary>The title of a battery: what it is for.</summary>
    /// <param name="kind">The check kind.</param>
    public static string TitleOf(CheckKind kind) => kind switch
    {
        CheckKind.Pre => "Check: before the game",
        CheckKind.Post => "Check: after the game",
        CheckKind.Recall => "Check: recall",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown check kind."),
    };

    /// <summary>The panel's rectangle in a window of this size (centred, at most 820 pixels wide).</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    public static Rectangle PanelFor(int viewportWidth, int viewportHeight)
    {
        var width = Math.Min(820, viewportWidth - 48);
        var height = Math.Min(430, viewportHeight - 48);
        return new Rectangle((viewportWidth - width) / 2, (viewportHeight - height) / 2, width, height);
    }

    /// <summary>The rectangle of option <paramref name="index"/> (from 0) in the panel: a row of 56 pixels.</summary>
    /// <param name="panel">The panel.</param>
    /// <param name="index">The option.</param>
    public static Rectangle OptionRow(Rectangle panel, int index) => new(panel.Left + 40, panel.Top + 150 + (index * 66), panel.Width - 80, 56);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var check = view.Check;
        if (check is null)
        {
            return;
        }

        var pixel = view.Pixel;
        var small = view.Fonts.Get(FontProvider.NativeSize);
        var title = view.Fonts.Get(TitleSize);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), new Color(6, 6, 14, 190));
        var panel = PanelFor(view.ViewportWidth, view.ViewportHeight);
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);
        DrawHelpers.Text(spriteBatch, title, TitleOf(check.Kind), new Vector2(panel.Left + 40, panel.Top + 18), Gold);
        var progress = check.IsDone ? "Done" : $"Item {check.Index + 1} of {check.Plan.Count}";
        DrawHelpers.Text(spriteBatch, small, progress, new Vector2(panel.Right - 40 - DrawHelpers.Width(small, progress), panel.Top + 26), Dim);

        if (check.Current is { } entry && !check.IsDone)
        {
            if (entry.Item.Kind == CheckItemKind.Comprehension)
            {
                DrawComprehension(spriteBatch, view, entry, panel, title, small);
            }
            else
            {
                DrawProduction(spriteBatch, pixel, check, entry, panel, title, small);
            }
        }
        else
        {
            var answered = check.Results.Count(static r => r.Input != CheckInput.Skipped);
            var skipped = check.Results.Count(static r => r.Input == CheckInput.Skipped);
            DrawHelpers.Text(spriteBatch, title, "Thank you.", new Vector2(panel.Left + 40, panel.Top + 150), Good);
            DrawHelpers.Text(spriteBatch, small, $"{answered} answered, {skipped} skipped.", new Vector2(panel.Left + 40, panel.Top + 200), Dim);
        }

        if (check.Notice is { } notice)
        {
            DrawHelpers.Text(spriteBatch, small, notice, new Vector2(panel.Left + 40, panel.Bottom - 74), Warn);
        }

        var thinking = check.Phase == CheckPhase.Thinking;
        var footer = thinking ? "Thinking..." : check.IsDone ? "The answers are in the session log." : "Esc: end the check (the rest is skipped)";
        DrawHelpers.Text(spriteBatch, small, footer, new Vector2(panel.Left + 40, panel.Bottom - 40), thinking ? Gold : Dim);
    }

    private static void DrawComprehension(SpriteBatch spriteBatch, FrameView view, CheckPlanEntry entry, Rectangle panel, SpriteFontBase title, SpriteFontBase small)
    {
        DrawHelpers.Text(spriteBatch, title, entry.Item.Question, new Vector2(panel.Left + 40, panel.Top + 66), Ink);
        DrawHelpers.Text(spriteBatch, small, "The line plays once (R hears it again). Press 1, 2 or 3 for the meaning.", new Vector2(panel.Left + 40, panel.Top + 110), Dim);
        var mouse = view.Mouse;
        var shown = entry.ShownOptions;
        for (var i = 0; i < shown.Count; i++)
        {
            var row = OptionRow(panel, i);
            var hovered = mouse is { } at && row.Contains(at.X, at.Y);
            spriteBatch.Draw(view.Pixel, row, hovered ? new Color(60, 60, 96, 245) : new Color(34, 34, 58, 235));
            DrawHelpers.Border(spriteBatch, view.Pixel, row, 1, new Color(110, 110, 150));
            var textX = row.Left + 16;
            if (shown[i].Icon is { } name && Enum.TryParse<ItemId>(name, out var item))
            {
                view.VoxelIcons.DrawIcon(spriteBatch, item, new Rectangle(row.Left + 12, row.Top + 8, 40, 40), 1);
                textX = row.Left + 64;
            }

            DrawHelpers.Text(spriteBatch, title, $"{i + 1}  {shown[i].Label}", new Vector2(textX, row.Top + 12), Ink);
        }
    }

    private static void DrawProduction(SpriteBatch spriteBatch, Texture2D pixel, CheckScreen check, CheckPlanEntry entry, Rectangle panel, SpriteFontBase title, SpriteFontBase small)
    {
        DrawHelpers.Text(spriteBatch, small, "Say it in Spanish, the way you would to the person in this situation.", new Vector2(panel.Left + 40, panel.Top + 66), Dim);
        DrawHelpers.Text(spriteBatch, title, entry.Item.Situation, new Vector2(panel.Left + 40, panel.Top + 100), Ink);
        DrawHelpers.Text(spriteBatch, small, "Hold V and speak, or W to type. N: I don't know (no sé). Enter sends what you typed.", new Vector2(panel.Left + 40, panel.Top + 160), Dim);
        var box = new Rectangle(panel.Left + 40, panel.Top + 200, panel.Width - 80, 44);
        spriteBatch.Draw(pixel, box, new Color(10, 10, 20, 255));
        var typed = check.Entry.Text;
        if (typed.Length > 0 || check.IsTyping)
        {
            DrawHelpers.Text(spriteBatch, title, check.IsTyping ? typed + "|" : typed, new Vector2(box.Left + 12, box.Top + 6), Ink);
        }
        else
        {
            DrawHelpers.Text(spriteBatch, small, check.Listening ? "Listening..." : "Your answer appears here.", new Vector2(box.Left + 12, box.Top + 12), Dim);
        }
    }
}
