using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The speaker's bubble (S15, reference B): while a conversation is open and the speaker's head is on the screen, a small dark rounded panel hangs over it with the
/// name in a warm colour over the line the speaker said last, and a pointer to the head. The panel along the bottom keeps every line, the buttons and the hints; this
/// is the line where the eye already is. Its place comes from <see cref="ConversationFrame"/>, which keeps the cards clear of it (S22).
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
        if (!dialogue.IsOpen)
        {
            return;
        }

        var frame = ConversationFrame.For(view);
        if (frame.Bubble is not { } layout)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var rows = frame.BubbleRows;
        var lineHeight = DialogueLayout.LineHeight;
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
