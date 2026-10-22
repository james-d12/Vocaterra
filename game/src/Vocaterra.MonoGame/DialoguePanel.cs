using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The dialogue panel along the bottom of the window: the lines so far (what the player said, the verdict note, the NPC's correction and reply, the
/// game's line with the world facts), the persistent <c>Oí: «…» · Esperaba: «…»</c> line after a spoken turn, the five speech buttons (Repetir,
/// Más despacio, Ayuda, No sé, Escribir), the choices the repair ladder offered, the text box while typing and a line of key hints. Its status
/// (<c>Escuchando…</c>, <c>Pensando…</c>) sits in the title bar. It only draws the state of the <see cref="DialogueModel"/>; the keys and the
/// mouse are handled by the game, which finds the buttons with the same <see cref="DialogueLayout"/> the drawing uses.
/// </summary>
public sealed class DialoguePanel : IFrameLayer
{
    /// <summary>The key hints under the text box while typing.</summary>
    public const string KeyHints = "Enter: enviar  Tab o Esc: botones  Alt+vocal: á é í ó ú  Alt+N: ñ  Alt+/: ¿  Alt+1: ¡";

    /// <summary>The key hints while the speech buttons are shown.</summary>
    public const string ButtonHints = "Mantén V o Espacio: hablar  Esc: cancelar o cerrar";

    /// <summary>The status while the push-to-talk key is held and the microphone records.</summary>
    public const string ListeningText = "Escuchando…";

    /// <summary>The status while a turn is on the worker (recognizing and evaluating).</summary>
    public const string ThinkingText = "Pensando…";

    /// <summary>The prefix of a line the player said, as the recognizer heard it.</summary>
    public const string SpokenPrefix = "Tú (voz): ";

    /// <summary>The guide's name in the panel's title bar (the title shows the name of the NPC the panel is open on, <see cref="DialogueModel.NpcName"/>).</summary>
    public const string Title = "Guía";

    private static readonly Color Background = new(14, 14, 28, 232);
    private static readonly Color Frame = new(255, 214, 64);
    private static readonly Color PlayerColor = new(150, 205, 255);
    private static readonly Color NoteColor = new(140, 140, 150);
    private static readonly Color CorrectionColor = new(255, 190, 110);
    private static readonly Color GuideColor = new(255, 242, 180);
    private static readonly Color WorldColor = new(170, 255, 190);
    private static readonly Color HintColor = new(255, 214, 64);
    private static readonly Color ListeningColor = new(120, 255, 150);
    private static readonly Color NoticeColor = new(255, 150, 120);
    private static readonly Color HeardColor = new(190, 225, 255);
    private static readonly Color ButtonBack = new(34, 34, 58, 235);
    private static readonly Color ButtonHover = new(60, 60, 96, 245);
    private static readonly Color ButtonText = new(236, 236, 246);
    private static readonly Color ChoiceBack = new(30, 52, 44, 240);
    private static readonly Color ChoiceBorder = new(120, 220, 130);

    /// <summary>The layout of the panel for the model's state and a window, measured in the panel's font: what the drawing and the mouse hit-test share.</summary>
    /// <param name="dialogue">The panel's state.</param>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="font">The font the panel draws with.</param>
    public static DialogueLayout LayoutFor(DialogueModel dialogue, int viewportWidth, int viewportHeight, FontStashSharp.SpriteFontBase font)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(font);
        return new DialogueLayout(viewportWidth, viewportHeight, text => DrawHelpers.Width(font, text), dialogue.Choices.Select(static c => c.Label).ToList(), dialogue.IsTyping);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var dialogue = view.Dialogue;
        if (!dialogue.IsOpen)
        {
            return;
        }

        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var layout = LayoutFor(dialogue, view.ViewportWidth, view.ViewportHeight, font);
        var panel = ToRectangle(layout.Panel);
        spriteBatch.Draw(pixel, panel, Background);
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Frame);

        var textLeft = panel.Left + DialogueLayout.Padding;
        var textWidth = layout.InnerWidth;
        DrawHelpers.Text(spriteBatch, font, dialogue.NpcName, new Vector2(textLeft, panel.Top + 8), Frame);
        spriteBatch.Draw(pixel, new Rectangle(textLeft, panel.Top + 8 + DialogueLayout.LineHeight, textWidth, 1), new Color(255, 214, 64, 120));
        DrawStatus(spriteBatch, view, font, dialogue, panel);
        DrawLines(spriteBatch, view, font, dialogue, textLeft, textWidth, layout.LinesTop, layout.LinesBottom);

        if (dialogue.Heard is { } heard)
        {
            var wrapped = TextWrap.Wrap(heard.Text, textWidth, t => DrawHelpers.Width(font, t));
            DrawHelpers.Text(spriteBatch, font, wrapped[0], new Vector2(textLeft, layout.HeardY), HeardColor);
        }

        var mouse = view.Mouse;
        foreach (var (action, box) in layout.Buttons)
        {
            var button = SpeechButtons.All.First(b => b.Action == action);
            var focused = dialogue.TypingOffered && action == SpeechAction.Type;
            DrawButton(spriteBatch, view, font, box, button.Text, mouse, focused ? HintColor : null, ButtonBack);
        }

        for (var i = 0; i < layout.Choices.Count; i++)
        {
            DrawButton(spriteBatch, view, font, layout.Choices[i].Box, DialogueLayout.ChoiceText(i, dialogue.Choices[i].Label), mouse, ChoiceBorder, ChoiceBack);
        }

        if (layout.InputBox is { } inputBox)
        {
            DrawInput(spriteBatch, view, font, dialogue, ToRectangle(inputBox));
        }

        DrawHelpers.Text(spriteBatch, font, dialogue.IsTyping ? KeyHints : ButtonHints, new Vector2(textLeft, layout.HintsY), NoteColor);
    }

    private static Rectangle ToRectangle(PixelBox box) => new(box.X, box.Y, box.Width, box.Height);

    private static void DrawButton(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, PixelBox box, string text, Point? mouse, Color? emphasis, Color back)
    {
        var rect = ToRectangle(box);
        var hovered = mouse is { } at && box.Contains(at.X, at.Y);
        spriteBatch.Draw(view.Pixel, rect, hovered ? ButtonHover : back);
        var border = emphasis ?? new Color(110, 110, 150);
        DrawHelpers.Border(spriteBatch, view.Pixel, rect, emphasis is null && !hovered ? 1 : 2, hovered && emphasis is null ? Frame : border);
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(rect.Left + DialogueLayout.ButtonPadding, rect.Top + 5), ButtonText);
    }

    private static void DrawInput(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, DialogueModel dialogue, Rectangle inputBox)
    {
        var pixel = view.Pixel;
        spriteBatch.Draw(pixel, inputBox, new Color(0, 0, 0, 170));
        DrawHelpers.Border(spriteBatch, pixel, inputBox, 1, new Color(255, 214, 64, 160));
        var prompt = "> ";
        var entry = dialogue.Entry;
        var promptWidth = DrawHelpers.Width(font, prompt);
        var origin = new Vector2(inputBox.Left + 8, inputBox.Top + 5);
        var innerWidth = inputBox.Width - 16 - promptWidth;
        var (visible, caretOffset) = TextWrap.Window(entry.Text, entry.Caret, innerWidth, t => DrawHelpers.Width(font, t));
        var caretX = DrawHelpers.Width(font, visible[..caretOffset]);
        DrawHelpers.Text(spriteBatch, font, prompt, origin, PlayerColor);
        DrawHelpers.Text(spriteBatch, font, visible, origin + new Vector2(promptWidth, 0), Color.White);
        if ((view.Tick / 30) % 2 == 0)
        {
            spriteBatch.Draw(pixel, new Rectangle((int)(origin.X + promptWidth + caretX), inputBox.Top + 4, 2, 20), Color.White);
        }
    }

    // "Escuchando…" while the key is held, "Pensando…" while a turn is on the worker, else the last microphone message; right-aligned in the title bar.
    private static void DrawStatus(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, DialogueModel dialogue, Rectangle panel)
    {
        var (text, color) = dialogue.Listening
            ? (ListeningText, ListeningColor)
            : dialogue.IsThinking
                ? (ThinkingText, HintColor)
                : dialogue.SpeechNotice is { } notice ? (notice, NoticeColor) : (string.Empty, Color.White);
        if (text.Length == 0)
        {
            return;
        }

        var x = panel.Right - DialogueLayout.Padding - DrawHelpers.Width(font, text);
        if (dialogue.Listening && (view.Tick / 15) % 2 == 0)
        {
            spriteBatch.Draw(view.Pixel, new Rectangle((int)x - 16, panel.Top + 13, 8, 8), new Color(255, 70, 70));
        }

        DrawHelpers.Text(spriteBatch, font, text, new Vector2(x, panel.Top + 8), color);
    }

    private static void DrawLines(
        SpriteBatch spriteBatch,
        FrameView view,
        FontStashSharp.SpriteFontBase font,
        DialogueModel dialogue,
        int left,
        int width,
        int top,
        int bottom)
    {
        // Wrap every line, then show the newest rows that fit. The line Repetir or Más despacio showed again is drawn on a gold plate.
        var rows = new List<(string Text, Color Color, bool Highlight)>();
        for (var index = 0; index < dialogue.Lines.Count; index++)
        {
            var line = dialogue.Lines[index];
            var prefix = line.Speaker switch
            {
                DialogueSpeaker.Player => "Tú: ",
                DialogueSpeaker.PlayerSpoken => SpokenPrefix,
                DialogueSpeaker.Guide or DialogueSpeaker.World or DialogueSpeaker.Correction => dialogue.NpcName + ": ",
                _ => string.Empty,
            };
            var color = line.Speaker switch
            {
                DialogueSpeaker.Player or DialogueSpeaker.PlayerSpoken => PlayerColor,
                DialogueSpeaker.Note => NoteColor,
                DialogueSpeaker.Correction => CorrectionColor,
                DialogueSpeaker.World => WorldColor,
                DialogueSpeaker.Hint => HintColor,
                _ => GuideColor,
            };
            var highlighted = index == dialogue.HighlightedLine && view.Tick < dialogue.HighlightUntilTick;
            var wrapped = TextWrap.Wrap(prefix + line.Text, width, t => DrawHelpers.Width(font, t));
            foreach (var row in wrapped)
            {
                rows.Add((row, highlighted ? Color.White : color, highlighted));
            }
        }

        var capacity = Math.Max(1, (bottom - top) / DialogueLayout.LineHeight);
        var first = Math.Max(0, rows.Count - capacity);
        var y = bottom - ((rows.Count - first) * DialogueLayout.LineHeight);
        for (var i = first; i < rows.Count; i++)
        {
            if (rows[i].Highlight)
            {
                spriteBatch.Draw(view.Pixel, new Rectangle(left - 4, y - 1, width + 8, DialogueLayout.LineHeight), new Color(120, 96, 20, 200));
            }

            DrawHelpers.Text(spriteBatch, font, rows[i].Text, new Vector2(left, y), rows[i].Color);
            y += DialogueLayout.LineHeight;
        }
    }
}
