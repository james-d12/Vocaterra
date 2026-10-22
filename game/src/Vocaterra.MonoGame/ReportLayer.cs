using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Reports;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The bug and feature report panel (S21, D-game-17), drawn in the dialogue panel's style: a dark rounded panel with a warm title, the Bug and
/// Feature request choices, the multi-line text box and the Save and Cancel buttons. It draws the <see cref="ReportPanel"/>'s state; the keys and
/// the clicks are the game's, and the geometry is <see cref="ReportLayout"/>'s.
/// </summary>
public sealed class ReportLayer : IFrameLayer
{
    /// <summary>The panel's title (English, D-game-13).</summary>
    public const string Title = "Report a bug or a feature request";

    /// <summary>The hints under the text box: the keys of the panel.</summary>
    public const string HintsFirst = "Ctrl+Enter: save report   Esc: cancel   Tab: Bug / Feature request   Enter: new line";

    /// <summary>The second hints line: the accents, as the dialogue's.</summary>
    public const string HintsSecond = "Alt+vowel: á é í ó ú   Alt+N: ñ   Alt+/: ¿   Alt+1: ¡";

    /// <summary>The text box's placeholder while it is empty.</summary>
    public const string Placeholder = "Describe what happened, or what you would like. Spanish is fine too.";

    private static readonly Color Scrim = new(0, 0, 0, 90);
    private static readonly Color Background = new(16, 16, 24, 240);
    private static readonly Color PanelEdge = new(112, 114, 136);
    private static readonly Color NameColor = DialoguePanel.NameColor;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color ButtonBack = new(34, 34, 58, 235);
    private static readonly Color ButtonHover = new(60, 60, 96, 245);
    private static readonly Color ButtonSelected = new(70, 62, 110, 245);
    private static readonly Color ButtonText = new(236, 236, 246);
    private static readonly Color NoteColor = new(140, 140, 150);
    private static readonly Color NoticeColor = new(255, 150, 120);
    private static readonly Color PlaceholderColor = new(120, 120, 140);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var panel = view.Report;
        if (panel is null || !panel.IsOpen)
        {
            return;
        }

        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var layout = new ReportLayout(view.ViewportWidth, view.ViewportHeight);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), Scrim);
        var box = ToRectangle(layout.Panel);
        DrawHelpers.RoundedPanel(spriteBatch, pixel, box, 9, Background, PanelEdge, 2);
        var left = box.Left + ReportLayout.Padding;
        DrawHelpers.Text(spriteBatch, font, Title, new Vector2(left, layout.TitleY), NameColor);

        var mouse = view.Mouse;
        DrawChoice(spriteBatch, view, font, layout.BugButton, ReportKinds.Label(ReportKind.Bug), panel.Kind == ReportKind.Bug, mouse);
        DrawChoice(spriteBatch, view, font, layout.FeatureButton, ReportKinds.Label(ReportKind.Feature), panel.Kind == ReportKind.Feature, mouse);
        DrawText(spriteBatch, view, font, panel, layout);
        DrawButton(spriteBatch, view, font, layout.CancelButton, "Cancel", mouse, ButtonBack);
        DrawButton(spriteBatch, view, font, layout.SaveButton, "Save report", mouse, ButtonBack);

        var hintsLeft = left;
        if (panel.Notice is { } notice)
        {
            DrawHelpers.Text(spriteBatch, font, notice, new Vector2(hintsLeft, layout.HintsY), NoticeColor);
        }
        else
        {
            DrawHelpers.Text(spriteBatch, font, HintsFirst, new Vector2(hintsLeft, layout.HintsY), NoteColor);
        }

        DrawHelpers.Text(spriteBatch, font, HintsSecond, new Vector2(hintsLeft, layout.HintsY + ReportLayout.LineHeight), NoteColor);
    }

    private static void DrawChoice(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, PixelBox box, string label, bool selected, Point? mouse)
    {
        var rect = ToRectangle(box);
        var hovered = mouse is { } at && box.Contains(at.X, at.Y);
        var fill = selected ? ButtonSelected : hovered ? ButtonHover : ButtonBack;
        var border = selected ? Gold : new Color(110, 110, 150);
        DrawHelpers.RoundedPanel(spriteBatch, view.Pixel, rect, 5, fill, border, selected ? 2 : 1);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(rect.Left + 10, rect.Top + 5), selected ? Gold : ButtonText);
    }

    private static void DrawButton(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, PixelBox box, string label, Point? mouse, Color back)
    {
        var rect = ToRectangle(box);
        var hovered = mouse is { } at && box.Contains(at.X, at.Y);
        DrawHelpers.RoundedPanel(spriteBatch, view.Pixel, rect, 5, hovered ? ButtonHover : back, hovered ? Gold : new Color(110, 110, 150), hovered ? 2 : 1);
        DrawHelpers.Text(spriteBatch, font, label, new Vector2(rect.Left + 12, rect.Top + 5), ButtonText);
    }

    // The text box: the wrapped rows, scrolled so that the caret's row is the last one shown; the caret blinks; an empty box says what to write.
    private static void DrawText(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, ReportPanel panel, ReportLayout layout)
    {
        var box = ToRectangle(layout.TextBox);
        var pixel = view.Pixel;
        spriteBatch.Draw(pixel, box, new Color(0, 0, 0, 170));
        DrawHelpers.Border(spriteBatch, pixel, box, 1, new Color(255, 214, 64, 160));
        var text = panel.Entry.Text;
        var origin = new Vector2(box.Left + 8, box.Top + 8);
        if (text.Length == 0)
        {
            DrawHelpers.Text(spriteBatch, font, Placeholder, origin, PlaceholderColor);
        }

        var rows = ReportWrap.Rows(text, layout.InnerWidth, t => DrawHelpers.Width(font, t));
        var caret = panel.Entry.Caret;
        var caretRow = ReportWrap.RowOf(rows, caret);
        var first = Math.Max(0, caretRow - layout.VisibleRows + 1);
        for (var i = first; i < rows.Count && i - first < layout.VisibleRows; i++)
        {
            var (start, length) = rows[i];
            DrawHelpers.Text(spriteBatch, font, text.Substring(start, length), origin + new Vector2(0, (i - first) * ReportLayout.LineHeight), Color.White);
        }

        if ((view.Tick / 30) % 2 == 0 && caretRow >= first && caretRow - first < layout.VisibleRows)
        {
            var (start, _) = rows[caretRow];
            var x = DrawHelpers.Width(font, text.Substring(start, caret - start));
            spriteBatch.Draw(pixel, new Rectangle((int)(origin.X + x), (int)origin.Y + ((caretRow - first) * ReportLayout.LineHeight), 2, ReportLayout.LineHeight - 2), Color.White);
        }
    }

    private static Rectangle ToRectangle(PixelBox box) => new(box.X, box.Y, box.Width, box.Height);
}
