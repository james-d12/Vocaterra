using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Dialogue;

/// <summary>
/// The dialogue panel along the bottom of the window: the lines so far (what the player said, the verdict note, the NPC's correction and reply, the
/// game's line with the world facts), the persistent <c>Heard: «…» · Expected: «…»</c> line after a spoken turn, the five speech buttons (Repetir,
/// Más despacio, Ayuda, No sé, Escribir), the choices the repair ladder offered, the text box while typing and a line of key hints. Its status
/// (<c>Escuchando…</c>, <c>Pensando…</c>) sits in the title bar. It only draws the state of the <see cref="DialogueModel"/>; the keys and the
/// mouse are handled by the game, which finds the buttons with the same <see cref="DialogueLayout"/> the drawing uses.
/// </summary>
public sealed class DialoguePanel : IFrameLayer
{
    /// <summary>
    /// The key hints under the text box while typing (S17b). V types a letter in the text box, so the talk key is named with the way back to the buttons: the
    /// talk key works from the buttons, and Tab or Esc returns there.
    /// </summary>
    public const string KeyHints = "Enter: send   Tab or Esc: buttons, then hold V or Space: speak   Alt+vowel: á é í ó ú   Alt+N: ñ   Alt+/: ¿   Alt+1: ¡";

    /// <summary>The key hints while the speech buttons are shown (S17b): the talk key is named in both states.</summary>
    public const string ButtonHints = "Hold V or Space: speak   Esc: cancel or close";

    /// <summary>The key hints when speech is unavailable, while the speech buttons are shown (S17b): the typed answer is the way to answer.</summary>
    public const string SpeechOffButtonHints = "Speech off: type your answer (W or Tab)   Esc: close";

    /// <summary>The key hints when speech is unavailable, while typing (S17b).</summary>
    public const string SpeechOffKeyHints = "Speech off: type your answer   Enter: send   Tab or Esc: buttons   Alt+vowel: á é í ó ú   Alt+N: ñ   Alt+/: ¿   Alt+1: ¡";

    /// <summary>The status while the push-to-talk key is held and the microphone records.</summary>
    public const string ListeningText = "Listening…";

    /// <summary>The status while a turn is on the worker (recognizing and evaluating).</summary>
    public const string ThinkingText = "Thinking…";

    /// <summary>The prefix of a line the player said, as the recognizer heard it.</summary>
    public const string SpokenPrefix = "You (voice): ";

    /// <summary>The guide's name in the panel's title bar (the title shows the name of the NPC the panel is open on, <see cref="DialogueModel.NpcName"/>).</summary>
    public const string Title = "Guía";

    /// <summary>The mark before every line of English in the panel (S16): the English is never spoken, so the panel says which line it is.</summary>
    public const string EnglishPrefix = "English: ";

    private static readonly Color Background = new(16, 16, 24, 236);
    private static readonly Color PanelEdge = new(112, 114, 136);

    /// <summary>The warm colour of the speaker's name over the line (S15, reference B).</summary>
    public static readonly Color NameColor = new(255, 204, 110);
    private static readonly Color Frame = new(255, 214, 64);
    private static readonly Color PlayerColor = new(150, 205, 255);
    private static readonly Color NoteColor = UiPalette.Muted.ToColor();
    private static readonly Color CorrectionColor = new(255, 190, 110);
    private static readonly Color GuideColor = new(255, 242, 180);
    private static readonly Color WorldColor = new(170, 255, 190);
    private static readonly Color HintColor = new(255, 214, 64);
    private static readonly Color ListeningColor = new(120, 255, 150);
    private static readonly Color NoticeColor = new(255, 150, 120);
    private static readonly Color HeardColor = new(190, 225, 255);

    /// <summary>The colour of every line of English in the game (S16, D-game-13): different from the Spanish lines' colours, so the two are never mistaken.</summary>
    public static readonly Color EnglishColor = new(226, 176, 255);
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
    /// <param name="session">The game, whose counts say whether a button still carries its English (S17), or <see langword="null"/> for the Spanish labels alone.</param>
    public static DialogueLayout LayoutFor(DialogueModel dialogue, int viewportWidth, int viewportHeight, FontStashSharp.SpriteFontBase font, GuideSession? session = null)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(font);
        return new DialogueLayout(viewportWidth, viewportHeight, text => DrawHelpers.Width(font, text), dialogue.Choices.Select(static c => c.Label).ToList(), dialogue.IsTyping, button => SpeechButtons.TextFor(button, session), HintsFor(dialogue.IsTyping, dialogue.SpeechUnavailable));
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
        var layout = LayoutFor(dialogue, view.ViewportWidth, view.ViewportHeight, font, view.Session);
        var panel = ToRectangle(layout.Panel);
        DrawHelpers.RoundedPanel(spriteBatch, pixel, panel, 9, Background, PanelEdge, 2);

        var textLeft = panel.Left + DialogueLayout.Padding;
        var textWidth = layout.InnerWidth;
        DrawHelpers.Text(spriteBatch, font, dialogue.NpcName, new Vector2(textLeft, panel.Top + 8), NameColor);
        spriteBatch.Draw(pixel, new Rectangle(textLeft, panel.Top + 8 + DialogueLayout.LineHeight, textWidth, 1), new Color(255, 204, 110, 90));
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
            DrawButton(spriteBatch, view, font, box, layout.ButtonsGlossed ? SpeechButtons.TextFor(button, view.Session) : button.Text, mouse, focused ? HintColor : null, ButtonBack);
        }

        for (var i = 0; i < layout.Choices.Count; i++)
        {
            DrawButton(spriteBatch, view, font, layout.Choices[i].Box, DialogueLayout.ChoiceText(i, dialogue.Choices[i].Label), mouse, ChoiceBorder, ChoiceBack);
        }

        if (layout.InputBox is { } inputBox)
        {
            DrawInput(spriteBatch, view, font, dialogue, ToRectangle(inputBox));
        }

        if (layout.HasHints)
        {
            for (var i = 0; i < layout.HintRows.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, layout.HintRows[i], new Vector2(textLeft, layout.HintsY + (i * DialogueLayout.LineHeight)), NoteColor);
            }
        }
    }

    /// <summary>
    /// The key hints line (S17b): it names the talk key in both states, and says that speech is off, with the typed answer, when the models are not loaded.
    /// </summary>
    /// <param name="typing">Whether the text box has the keys.</param>
    /// <param name="speechUnavailable">Whether the speech models are not loaded (the talk key does nothing).</param>
    public static string HintsFor(bool typing, bool speechUnavailable) => (typing, speechUnavailable) switch
    {
        (true, false) => KeyHints,
        (true, true) => SpeechOffKeyHints,
        (false, false) => ButtonHints,
        _ => SpeechOffButtonHints,
    };

    private static Rectangle ToRectangle(PixelBox box) => new(box.X, box.Y, box.Width, box.Height);

    private static void DrawButton(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, PixelBox box, string text, Point? mouse, Color? emphasis, Color back)
    {
        var rect = ToRectangle(box);
        var hovered = mouse is { } at && box.Contains(at.X, at.Y);
        var border = emphasis ?? new Color(110, 110, 150);
        DrawHelpers.RoundedPanel(spriteBatch, view.Pixel, rect, 5, hovered ? ButtonHover : back, hovered && emphasis is null ? Frame : border, emphasis is null && !hovered ? 1 : 2);
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
                DialogueSpeaker.Player => "You: ",
                DialogueSpeaker.PlayerSpoken => SpokenPrefix,
                DialogueSpeaker.Guide or DialogueSpeaker.World or DialogueSpeaker.Correction => dialogue.NpcName + ": ",
                DialogueSpeaker.English => EnglishPrefix,
                _ => string.Empty,
            };
            var color = line.Speaker switch
            {
                DialogueSpeaker.Player or DialogueSpeaker.PlayerSpoken => PlayerColor,
                DialogueSpeaker.Note => NoteColor,
                DialogueSpeaker.Correction => CorrectionColor,
                DialogueSpeaker.World => WorldColor,
                DialogueSpeaker.Hint => HintColor,
                DialogueSpeaker.English => EnglishColor,
                _ => GuideColor,
            };
            var highlighted = index == dialogue.HighlightedLine && view.Tick < dialogue.HighlightUntilTick;
            var wrapped = TextWrap.Wrap(prefix + line.Text, width, t => DrawHelpers.Width(font, t));
            foreach (var row in wrapped)
            {
                rows.Add((row, highlighted ? Color.White : color, highlighted));
            }

            // The English under an NPC's Spanish line (S16, D-game-13): in its own colour and marked as English, while the phrase in play is at the
            // English rung or the help key was pressed, and always for a line no phrase is practising.
            if (line.Speaker == DialogueSpeaker.Guide && view.Session.EnglishForNpcLine(line.Text) is { } english)
            {
                foreach (var row in TextWrap.Wrap(EnglishPrefix + english, width, t => DrawHelpers.Width(font, t)))
                {
                    rows.Add((row, EnglishColor, false));
                }
            }
        }

        var capacity = (bottom - top) / DialogueLayout.LineHeight;
        if (capacity <= 0)
        {
            return; // a compact panel with no room: the bubble and the lesson strip carry the line (X08)
        }

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
