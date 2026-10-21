using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The dialogue panel along the bottom of the window: the lines so far (what the player typed, the verdict note, the guide's correction and
/// reply, the game's line with the world facts), the text box with its caret, and a line of key hints. It only draws the state of the
/// <see cref="DialogueModel"/>; the keys are handled by the game.
/// </summary>
public sealed class DialoguePanel : IFrameLayer
{
    /// <summary>The key hints under the text box.</summary>
    public const string KeyHints = "Enter: enviar  Tab (mantener): hablar  Esc: cerrar  Alt+vocal: á é í ó ú  Alt+N: ñ  Alt+/: ¿  Alt+1: ¡";

    /// <summary>The status while the push-to-talk key is held and the microphone records.</summary>
    public const string ListeningText = "Escuchando…";

    /// <summary>The status while a turn is on the worker (recognizing and evaluating).</summary>
    public const string ThinkingText = "Pensando…";

    /// <summary>The prefix of a line the player said, as the recognizer heard it.</summary>
    public const string SpokenPrefix = "Tú (voz): ";

    /// <summary>The guide's name in the panel's title bar.</summary>
    public const string Title = "Guía";

    private const int Margin = 16;
    private const int Padding = 12;
    private const int LineHeight = 20;
    private const int PanelHeight = 300;
    private const int MaxWidth = 980;

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

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var dialogue = view.Dialogue;
        if (!dialogue.IsOpen)
        {
            return;
        }

        var camera = view.Camera;
        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var width = Math.Min(MaxWidth, camera.ViewportWidth - (Margin * 2));
        var height = Math.Min(PanelHeight, camera.ViewportHeight - (Margin * 2));
        var panel = new Rectangle((camera.ViewportWidth - width) / 2, camera.ViewportHeight - height - Margin, width, height);
        spriteBatch.Draw(pixel, panel, Background);
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Frame);

        var textLeft = panel.Left + Padding;
        var textWidth = panel.Width - (Padding * 2);
        DrawHelpers.Text(spriteBatch, font, Title, new Vector2(textLeft, panel.Top + 8), Frame);
        spriteBatch.Draw(pixel, new Rectangle(panel.Left + Padding, panel.Top + 8 + LineHeight, textWidth, 1), new Color(255, 214, 64, 120));

        // The input row and the hints sit at the bottom; the lines fill what is above, newest last.
        var hintsY = panel.Bottom - Padding - LineHeight + 2;
        var inputBox = new Rectangle(textLeft, hintsY - 34, textWidth, 28);
        var linesTop = panel.Top + 8 + LineHeight + 6;
        var linesBottom = inputBox.Top - 6;
        DrawLines(spriteBatch, view, font, dialogue, textLeft, textWidth, linesTop, linesBottom);

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
        if ((view.Simulation.Tick / 30) % 2 == 0)
        {
            spriteBatch.Draw(pixel, new Rectangle((int)(origin.X + promptWidth + caretX), inputBox.Top + 4, 2, 20), Color.White);
        }

        DrawHelpers.Text(spriteBatch, font, KeyHints, new Vector2(textLeft, hintsY), NoteColor);
        DrawStatus(spriteBatch, view, font, dialogue, inputBox);
    }

    // "Escuchando…" while the key is held, "Pensando…" while a turn is on the worker, else the last microphone message; right-aligned in the box.
    private static void DrawStatus(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, DialogueModel dialogue, Rectangle box)
    {
        var (text, color) = dialogue.Listening
            ? (ListeningText, ListeningColor)
            : dialogue.Pending > 0
                ? (ThinkingText, HintColor)
                : dialogue.SpeechNotice is { } notice ? (notice, NoticeColor) : (string.Empty, Color.White);
        if (text.Length == 0)
        {
            return;
        }

        var x = box.Right - 10 - DrawHelpers.Width(font, text);
        if (dialogue.Listening && (view.Simulation.Tick / 15) % 2 == 0)
        {
            spriteBatch.Draw(view.Pixel, new Rectangle((int)x - 16, box.Top + 10, 8, 8), new Color(255, 70, 70));
        }

        DrawHelpers.Text(spriteBatch, font, text, new Vector2(x, box.Top + 5), color);
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
        // Wrap every line, then show the newest rows that fit.
        var rows = new List<(string Text, Color Color)>();
        foreach (var line in dialogue.Lines)
        {
            var prefix = line.Speaker switch
            {
                DialogueSpeaker.Player => "Tú: ",
                DialogueSpeaker.PlayerSpoken => SpokenPrefix,
                DialogueSpeaker.Guide or DialogueSpeaker.World or DialogueSpeaker.Correction => "Guía: ",
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
            var wrapped = TextWrap.Wrap(prefix + line.Text, width, t => DrawHelpers.Width(font, t));
            foreach (var row in wrapped)
            {
                rows.Add((row, color));
            }
        }

        var capacity = Math.Max(1, (bottom - top) / LineHeight);
        var first = Math.Max(0, rows.Count - capacity);
        var y = bottom - ((rows.Count - first) * LineHeight);
        for (var i = first; i < rows.Count; i++)
        {
            DrawHelpers.Text(spriteBatch, font, rows[i].Text, new Vector2(left, y), rows[i].Color);
            y += LineHeight;
        }
    }
}
