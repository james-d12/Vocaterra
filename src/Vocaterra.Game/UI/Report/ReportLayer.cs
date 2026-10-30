using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI.Report;

/// <summary>
/// The bug and feature report panel (S21, D-game-17), drawn in the dialogue panel's style: a dark rounded panel with a warm title, the Bug and
/// Feature request choices, the multi-line text box and the Save and Cancel buttons. It draws the <see cref="ReportPanel"/>'s state; the keys and
/// the clicks are the game's, and the geometry is <see cref="ReportLayout"/>'s.
/// </summary>
public sealed class ReportLayer : IFrameLayer
{
    /// <summary>The panel's title (English, D-game-13).</summary>
    /// <param name="ui">The support language (<c>ui.report*</c>, C04).</param>
    public static string Title(LocaleText ui) => Ui(ui).Ui("reportTitle");

    /// <summary>The hints under the text box: the keys of the panel.</summary>
    /// <param name="ui">The support language.</param>
    public static string HintsFirst(LocaleText ui) => Ui(ui).Ui("reportHintsFirst");

    /// <summary>The second hints line: the accents, as the dialogue's.</summary>
    /// <param name="ui">The support language.</param>
    public static string HintsSecond(LocaleText ui) => Ui(ui).Ui("reportHintsSecond");

    /// <summary>The text box's placeholder while it is empty.</summary>
    /// <param name="ui">The support language.</param>
    public static string Placeholder(LocaleText ui) => Ui(ui).Ui("reportPlaceholder");

    private static readonly Color Scrim = new(0, 0, 0, 90);
    private static readonly Color Background = new(16, 16, 24, 240);
    private static readonly Color PanelEdge = new(112, 114, 136);
    private static readonly Color NameColor = DialoguePanel.NameColor;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color ButtonBack = new(34, 34, 58, 235);
    private static readonly Color ButtonHover = new(60, 60, 96, 245);
    private static readonly Color ButtonSelected = new(70, 62, 110, 245);
    private static readonly Color ButtonText = new(236, 236, 246);
    private static readonly Color NoteColor = UiPalette.Muted.ToColor();
    private static readonly Color NoticeColor = new(255, 150, 120);
    private static readonly Color PlaceholderColor = UiPalette.Placeholder.ToColor();

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
        var layout = LayoutFor(view.ViewportWidth, view.ViewportHeight, font, view.Session.Locale);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), Scrim);
        var box = ToRectangle(layout.Panel);
        DrawHelpers.RoundedPanel(spriteBatch, pixel, box, 9, Background, PanelEdge, 2);
        var left = box.Left + ReportLayout.Padding;
        DrawHelpers.Text(spriteBatch, font, Title(view.Session.Locale), new Vector2(left, layout.TitleY), NameColor);

        var mouse = view.Mouse;
        DrawChoice(spriteBatch, view, font, layout.BugButton, view.Session.Locale.Ui("reportBug"), panel.Kind == ReportKind.Bug, mouse);
        DrawChoice(spriteBatch, view, font, layout.FeatureButton, view.Session.Locale.Ui("reportFeature"), panel.Kind == ReportKind.Feature, mouse);
        DrawText(spriteBatch, view, font, panel, layout);
        DrawButton(spriteBatch, view, font, layout.CancelButton, view.Session.Locale.Ui("reportCancel"), mouse, ButtonBack);
        DrawButton(spriteBatch, view, font, layout.SaveButton, view.Session.Locale.Ui("reportSave"), mouse, ButtonBack);

        var hintsLeft = left;
        var firstRows = panel.Notice is { } noticeId && view.Session.Locale.Ui(noticeId) is { } notice
            ? NoticeRows(notice, layout, font)
            : layout.FirstRows;
        var noticeShown = panel.Notice is not null;
        for (var i = 0; i < firstRows.Count; i++)
        {
            DrawHelpers.Text(spriteBatch, font, firstRows[i], new Vector2(hintsLeft, layout.HintsY + (i * ReportLayout.LineHeight)), noticeShown ? NoticeColor : NoteColor);
        }

        for (var i = 0; i < layout.SecondRows.Count; i++)
        {
            DrawHelpers.Text(spriteBatch, font, layout.SecondRows[i], new Vector2(hintsLeft, layout.SecondY + (i * ReportLayout.LineHeight)), NoteColor);
        }
    }

    /// <summary>The layout for a canvas in the panel's font: what the drawing and the mouse hit-test share (the hint rows depend on the font's widths).</summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="font">The font the panel draws with.</param>
    /// <param name="ui">The support language (the hints).</param>
    public static ReportLayout LayoutFor(int viewportWidth, int viewportHeight, FontStashSharp.SpriteFontBase font, LocaleText ui)
    {
        ArgumentNullException.ThrowIfNull(font);
        return new ReportLayout(viewportWidth, viewportHeight, text => DrawHelpers.Width(font, text), HintsFirst(ui), HintsSecond(ui));
    }

    private static LocaleText Ui(LocaleText ui) => ui ?? throw new ArgumentNullException(nameof(ui));

    // The notice wrapped into the rows the first hints line has (the buttons must not move when it comes and goes), the last row ending in an ellipsis when it is cut.
    private static List<string> NoticeRows(string notice, ReportLayout layout, FontStashSharp.SpriteFontBase font)
    {
        var rows = TextWrap.Wrap(notice, layout.Panel.Width - (2 * ReportLayout.Padding), text => DrawHelpers.Width(font, text)).ToList();
        if (rows.Count > layout.FirstRows.Count)
        {
            rows = rows.Take(layout.FirstRows.Count).ToList();
            rows[^1] = rows[^1].TrimEnd() + "…";
        }

        return rows;
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
            var holder = TextWrap.Wrap(Placeholder(view.Session.Locale), layout.InnerWidth, t => DrawHelpers.Width(font, t));
            for (var i = 0; i < holder.Count && i < layout.VisibleRows; i++)
            {
                DrawHelpers.Text(spriteBatch, font, holder[i], origin + new Vector2(0, i * ReportLayout.LineHeight), PlaceholderColor);
            }
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
