using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Content;
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
    /// <param name="ui">The support language (<c>ui.dialogueKeyHints</c>, C04).</param>
    public static string KeyHints(LocaleText ui) => Ui(ui).Ui("dialogueKeyHints");

    /// <summary>The key hints while the speech buttons are shown (S17b): the talk key is named in both states.</summary>
    /// <param name="ui">The support language.</param>
    public static string ButtonHints(LocaleText ui) => Ui(ui).Ui("dialogueButtonHints");

    /// <summary>The key hints when speech is unavailable, while the speech buttons are shown (S17b): the typed answer is the way to answer.</summary>
    /// <param name="ui">The support language.</param>
    public static string SpeechOffButtonHints(LocaleText ui) => Ui(ui).Ui("dialogueSpeechOffButtonHints");

    /// <summary>The key hints when speech is unavailable, while typing (S17b).</summary>
    /// <param name="ui">The support language.</param>
    public static string SpeechOffKeyHints(LocaleText ui) => Ui(ui).Ui("dialogueSpeechOffKeyHints");

    /// <summary>The status while the push-to-talk key is held and the microphone records.</summary>
    /// <param name="ui">The support language.</param>
    public static string ListeningText(LocaleText ui) => Ui(ui).Ui("dialogueListening");

    /// <summary>The status while a turn is on the worker (recognizing and evaluating).</summary>
    /// <param name="ui">The support language.</param>
    public static string ThinkingText(LocaleText ui) => Ui(ui).Ui("dialogueThinking");

    /// <summary>The prefix of a line the player said, as the recognizer heard it.</summary>
    /// <param name="ui">The support language.</param>
    public static string SpokenPrefix(LocaleText ui) => Ui(ui).Ui("dialogueYouSpoken");

    /// <summary>The mark before every line of English in the panel (S16): the English is never spoken, so the panel says which line it is.</summary>
    /// <param name="ui">The support language.</param>
    public static string SupportPrefix(LocaleText ui) => Ui(ui).Ui("supportPrefix");

    /// <summary>The width the header keeps free at its right for the status (Listening…, Thinking…, a notice), in pixels.</summary>
    public const int HeaderStatusRoom = 110;

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
        return new DialogueLayout(viewportWidth, viewportHeight, text => DrawHelpers.Width(font, text), dialogue.Choices.Select(static c => c.Label).ToList(), dialogue.Buttons, dialogue.IsTyping, button => SpeechButtons.TextFor(button, session), HintsFor(session?.Locale ?? throw new ArgumentNullException(nameof(session)), dialogue.IsTyping, dialogue.SpeechUnavailable, session.Tips.WasShown("speak")));
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
        // U05: the NPC's name is the panel's title, at twice the native size (sharp at whole multiples); a compact panel keeps the native size and its rows.
        var nameFont = NameFontFor(layout, view.Fonts);
        var nameTop = panel.Top + NameTop(layout);
        DrawHelpers.Text(spriteBatch, nameFont, dialogue.NpcName, new Vector2(textLeft, nameTop), NameColor);
        spriteBatch.Draw(pixel, new Rectangle(textLeft, panel.Top + layout.Header - 6, textWidth, 1), new Color(255, 204, 110, 90));
        DrawStatus(spriteBatch, view, font, dialogue, panel);
        DrawLessonHeading(spriteBatch, view, font, nameFont, dialogue, textLeft, panel, nameTop + (layout.Compact ? 0 : FontProvider.NativeSize));
        DrawLines(spriteBatch, view, font, dialogue, textLeft, textWidth, layout.LinesTop, layout.LinesBottom);

        if (dialogue.Heard is { } heard)
        {
            var wrapped = TextWrap.Wrap(heard.Text(view.Session.Locale), textWidth, t => DrawHelpers.Width(font, t));
            DrawHelpers.Text(spriteBatch, font, wrapped[0], new Vector2(textLeft, layout.HeardY), HeardColor);
        }

        var mouse = view.Mouse;
        foreach (var (action, box) in layout.Buttons)
        {
            var button = dialogue.Buttons.First(b => b.Action == action);
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
    /// <param name="ui">The support language.</param>
    /// <param name="typing">Whether the text box has the keys.</param>
    /// <param name="speechUnavailable">Whether the speech models are not loaded (the talk key does nothing).</param>
    /// <param name="speakHintShown">Whether the speak tip has been shown (U01): the footer then stops repeating how to speak (the keys stay in the controls help, F1).</param>
    public static string HintsFor(LocaleText ui, bool typing, bool speechUnavailable, bool speakHintShown = false) => (typing, speechUnavailable) switch
    {
        (true, false) when speakHintShown => Ui(ui).Ui("dialogueKeyHintsShort"),
        (false, false) when speakHintShown => Ui(ui).Ui("dialogueButtonHintsShort"),
        (true, false) => KeyHints(ui),
        (true, true) => SpeechOffKeyHints(ui),
        (false, false) => ButtonHints(ui),
        _ => SpeechOffButtonHints(ui),
    };

    /// <summary>The size the NPC's name is drawn at: twice the native size, or the native size on a compact panel (U05).</summary>
    /// <param name="layout">The panel's layout.</param>
    public static int NameSize(DialogueLayout layout) => layout.Compact ? FontProvider.NativeSize : FontProvider.NativeSize * 2;

    private static FontStashSharp.SpriteFontBase NameFontFor(DialogueLayout layout, FontProvider fonts) => fonts.Get(NameSize(layout));

    // The name's top under the panel's top: the 16 px name keeps its old place, the 32 px one sits two pixels higher so the rule has room under it.
    private static int NameTop(DialogueLayout layout) => layout.Compact ? 8 : 6;

    /// <summary>
    /// The prefix of a conversation line. The NPC's lines (the guide, the world facts, the correction) carry none since U05: the name is the panel's title
    /// and their colours set them apart. The learner's lines and the English lines keep theirs.
    /// </summary>
    /// <param name="speaker">Who the line is from.</param>
    /// <param name="ui">The support language.</param>
    public static string LinePrefix(DialogueSpeaker speaker, LocaleText ui) => speaker switch
    {
        DialogueSpeaker.Player => Ui(ui).Ui("dialogueYou"),
        DialogueSpeaker.PlayerSpoken => SpokenPrefix(ui),
        DialogueSpeaker.English => SupportPrefix(ui),
        _ => string.Empty,
    };

    private static LocaleText Ui(LocaleText ui) => ui ?? throw new ArgumentNullException(nameof(ui));

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
            ? (ListeningText(view.Session.Locale), ListeningColor)
            : dialogue.IsThinking
                ? (ThinkingText(view.Session.Locale), HintColor)
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

    // U02: the lesson's heading in the header, between the NPC's name and the status; the title is shortened first so the step and its kind stay (U02b).
    private static void DrawLessonHeading(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, FontStashSharp.SpriteFontBase nameFont, DialogueModel dialogue, int textLeft, Rectangle panel, int y)
    {
        if (view.LessonHeading is not { } heading)
        {
            return;
        }

        var x = textLeft + DrawHelpers.Width(nameFont, dialogue.NpcName) + 24;
        var room = panel.Right - DialogueLayout.Padding - HeaderStatusRoom - x;
        DrawHelpers.Text(spriteBatch, font, heading.FitTo(room, t => DrawHelpers.Width(font, t)), new Vector2(x, y), NoteColor);
    }

    // One wrapped row of the log: its text, its colour and whether it is the line a repeat or slow reply showed again (drawn on a gold plate).
    private readonly record struct PanelText(string Text, Color Color, bool Highlight);

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
        // Wrap every row, then fit the blocks to the room (U02b, PanelFitter): the page scrolls, the collapsed exchange keeps the learner's line and the NPC's Spanish.
        var blocks = new List<PanelBlock>();
        var spanishRows = new List<List<PanelText>>();
        var englishRows = new List<List<PanelText>>();
        foreach (var panelRow in dialogue.PanelRows())
        {
            var index = panelRow.Index;
            var line = panelRow.Line;
            var prefix = LinePrefix(line.Speaker, view.Session.Locale);
            var color = line.Speaker switch
            {
                DialogueSpeaker.Player or DialogueSpeaker.PlayerSpoken => PlayerColor,
                DialogueSpeaker.Note => NoteColor,
                DialogueSpeaker.Correction or DialogueSpeaker.Accent or DialogueSpeaker.Coaching => CorrectionColor,
                DialogueSpeaker.World => WorldColor,
                DialogueSpeaker.Hint => HintColor,
                DialogueSpeaker.English => EnglishColor,
                _ => GuideColor,
            };
            var highlighted = index >= 0 && index == dialogue.HighlightedLine && view.Tick < dialogue.HighlightUntilTick;
            var spanish = TextWrap.Wrap(prefix + line.Text, width, t => DrawHelpers.Width(font, t))
                .Select(row => new PanelText(row, highlighted ? Color.White : color, highlighted))
                .ToList();

            // The English under an NPC's Spanish line (S16, D-game-13): in its own colour and marked as English, while the phrase in play is at the
            // English rung or the help key was pressed, and always for a line no phrase is practising.
            var english = new List<PanelText>();
            if (line.Speaker == DialogueSpeaker.Guide && view.Session.SupportForNpcLine(dialogue.Npc, line.Text) is { } support)
            {
                english.AddRange(TextWrap.Wrap(SupportPrefix(view.Session.Locale) + support, width, t => DrawHelpers.Width(font, t))
                    .Select(row => new PanelText(row, EnglishColor, false)));
            }

            blocks.Add(new PanelBlock(index, line.Speaker, spanish.Count, english.Count));
            spanishRows.Add(spanish);
            englishRows.Add(english);
        }

        var capacity = (bottom - top) / DialogueLayout.LineHeight;
        if (capacity <= 0)
        {
            return; // a compact panel with no room: the bubble and the lesson strip carry the line (X08)
        }

        var fit = PanelFitter.Fit(blocks, capacity, dialogue.Expanded, dialogue.ScrollBack, dialogue.HiddenLineCount);
        var rows = new List<PanelText>();
        if (fit.ShowEarlier)
        {
            rows.Add(new PanelText(view.Session.Locale.Plural(dialogue.Expanded ? "ui.dialogueAbove" : "ui.dialogueOlder", fit.EarlierLines), NoteColor, false));
        }

        for (var i = fit.First; i < fit.End; i++)
        {
            if (!fit.Shows(i))
            {
                continue;
            }

            rows.AddRange(spanishRows[i]);
            if (fit.ShowEnglish[i])
            {
                rows.AddRange(englishRows[i]);
            }
        }

        if (fit.ShowNewer)
        {
            rows.Add(new PanelText(view.Session.Locale.Plural("ui.dialogueBelow", fit.NewerLines), NoteColor, false));
        }

        var shown = Math.Max(0, rows.Count - capacity); // a single line taller than the room loses its top rows
        var y = bottom - ((rows.Count - shown) * DialogueLayout.LineHeight);
        for (var i = shown; i < rows.Count; i++)
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
