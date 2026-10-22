using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The speaker's bubble (S15, reference B): while a conversation is open and the speaker's head is on the screen, a small dark rounded panel hangs over it with the
/// name in a warm colour over the line the speaker said last, and a pointer to the head. The panel along the bottom keeps every line, the buttons and the hints; this
/// is the line where the eye already is.
/// </summary>
public sealed class DialogueBubbleLayer : IFrameLayer
{
    private static readonly Color Back = new(16, 16, 24, 236);
    private static readonly Color Edge = new(112, 114, 136);
    private static readonly Color LineColour = new(246, 242, 232);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var dialogue = view.Dialogue;
        if (!dialogue.IsOpen || view.Camera3D is not { } camera || DialogueBubble.LineOf(dialogue) is not { } line)
        {
            return;
        }

        var speaker = view.Figures.FirstOrDefault(f => string.Equals(f.Name, dialogue.Npc, StringComparison.Ordinal));
        if (speaker is null)
        {
            return;
        }

        var head = camera.Screen.Project(speaker.Feet.X + 0.5, speaker.Feet.Y + 2.1, speaker.Feet.Z + 0.5);
        if (!head.InFront)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var rows = DialogueBubble.Rows(line, DialogueBubble.MaxWidth - (2 * DialogueBubble.Padding), t => DrawHelpers.Width(font, t));
        var widest = rows.Max(r => DrawHelpers.Width(font, r));
        var width = (int)Math.Min(DialogueBubble.MaxWidth, Math.Max(widest, DrawHelpers.Width(font, dialogue.NpcName)) + (2 * DialogueBubble.Padding));
        var lineHeight = DialogueLayout.LineHeight;
        var height = (2 * DialogueBubble.Padding) + ((rows.Count + 1) * lineHeight);
        if (DialogueBubble.Place(view.ViewportWidth, view.ViewportHeight, head.X, head.Y, width, height) is not { } layout)
        {
            return;
        }

        var box = new Rectangle(layout.Box.X, layout.Box.Y, layout.Box.Width, layout.Box.Height);
        DrawHelpers.RoundedPanel(spriteBatch, view.Pixel, box, 7, Back, Edge, 2);

        // The pointer: a small triangle under the box, made of shrinking rows, leaning from the box towards the head.
        for (var i = 0; i < DialogueBubble.TailHeight; i++)
        {
            var half = Math.Max(0, 6 - ((i * 6) / DialogueBubble.TailHeight));
            var x = layout.TailBaseX + ((layout.TailX - layout.TailBaseX) * i / DialogueBubble.TailHeight);
            spriteBatch.Draw(view.Pixel, new Rectangle(x - half, box.Bottom - 1 + i, (2 * half) + 1, 1), Back);
        }

        DrawHelpers.Text(spriteBatch, font, dialogue.NpcName, new Vector2(box.X + DialogueBubble.Padding, box.Y + DialogueBubble.Padding), DialoguePanel.NameColor);
        for (var i = 0; i < rows.Count; i++)
        {
            DrawHelpers.Text(spriteBatch, font, rows[i], new Vector2(box.X + DialogueBubble.Padding, box.Y + DialogueBubble.Padding + ((i + 1) * lineHeight)), LineColour);
        }
    }
}
