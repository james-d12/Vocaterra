using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The phrase support of S8, drawn above the dialogue panel while it is open: the support card of each phrase the NPC's prompt is asking for while its
/// level is <see cref="SupportLevel.CardAndAudio"/> (the phrase sheet's card text: the phrase, its meaning and the word glosses, with an audio slot), a
/// slim chip with the replay key while it is <see cref="SupportLevel.AudioOnly"/> (no card), and the stall's key-word cards (<c>el agua</c>,
/// <c>el pan</c>): large and centred while they stand alone before the shopkeeper's first question, small at the right of the row for the rest of
/// that talk. At <see cref="SupportLevel.None"/> nothing is drawn until the player asks for help.
/// </summary>
public sealed class SupportLayer : IFrameLayer
{
    /// <summary>The label on a support card.</summary>
    public const string CardTag = "Puedes decir";

    /// <summary>The text of the audio-only chip.</summary>
    public const string ChipText = "[R] Escuchar la frase";

    /// <summary>The heading over the key-word cards while they stand alone.</summary>
    public const string KeyWordsHeading = "Palabras nuevas";

    private const int PhraseSize = 32;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Dim = new(150, 150, 165);
    private static readonly Color Empty = new(90, 90, 110);
    private static readonly Color Back = new(12, 16, 30, 226);
    private static readonly Color CardBorder = new(120, 220, 130);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.Dialogue.IsOpen)
        {
            return;
        }

        var session = view.Session;
        var layout = SupportLayout.For(session, view.ViewportWidth, view.ViewportHeight);
        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(PhraseSize);
        var cards = session.Support.Cards;
        for (var i = 0; i < cards.Count && i < layout.Cards.Count; i++)
        {
            DrawCard(spriteBatch, view, small, large, cards[i], Rect(layout.Cards[i]));
        }

        var words = session.KeyWordCards;
        if (layout.KeyWordsAlone && words.Count > 0)
        {
            var heading = new Rectangle(layout.KeyWords[0].X, layout.HeadingY + 2, (int)DrawHelpers.Width(small, KeyWordsHeading) + 16, 20);
            spriteBatch.Draw(view.Pixel, heading, Back);
            DrawHelpers.Text(spriteBatch, small, KeyWordsHeading, new Vector2(heading.X + 8, heading.Y + 2), Gold);
        }

        for (var i = 0; i < words.Count && i < layout.KeyWords.Count; i++)
        {
            DrawKeyWord(spriteBatch, view, small, large, words[i], Rect(layout.KeyWords[i]), layout.KeyWordsAlone);
        }

        if (layout.Chip is { } chip)
        {
            var box = Rect(chip);
            spriteBatch.Draw(view.Pixel, box, Back);
            DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, Gold);
            DrawHelpers.Text(spriteBatch, small, ChipText, new Vector2(box.X + 10, box.Y + 6), Ink);
        }
    }

    private static Rectangle Rect(PixelBox box) => new(box.X, box.Y, box.Width, box.Height);

    private static void DrawCard(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase small, FontStashSharp.SpriteFontBase large, SupportCard card, Rectangle box)
    {
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, CardBorder);
        var left = box.X + 12;
        var width = box.Width - 24;
        DrawHelpers.Text(spriteBatch, small, CardTag, new Vector2(left, box.Y + 6), Gold);
        var slot = new Rectangle(box.Right - 62, box.Y + 6, 52, 22);
        spriteBatch.Draw(view.Pixel, slot, new Color(30, 30, 44));
        DrawHelpers.Border(spriteBatch, view.Pixel, slot, 1, Empty);
        DrawHelpers.Text(spriteBatch, small, "audio", new Vector2(slot.X + 6, slot.Y + 3), Empty);
        var phraseFont = DrawHelpers.Width(large, card.Phrase) <= width ? large : small;
        DrawHelpers.Text(spriteBatch, phraseFont, card.Phrase, new Vector2(left, box.Y + 22), Ink);
        DrawHelpers.Text(spriteBatch, small, card.Meaning, new Vector2(left, box.Y + 58), new Color(190, 225, 255));
        if (card.Glosses.Count > 0)
        {
            var rows = TextWrap.Wrap(string.Join(" · ", card.Glosses), width, t => DrawHelpers.Width(small, t));
            for (var i = 0; i < Math.Min(2, rows.Count); i++)
            {
                DrawHelpers.Text(spriteBatch, small, rows[i], new Vector2(left, box.Y + 76 + (i * 16)), Dim);
            }
        }
    }

    private static void DrawKeyWord(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase small, FontStashSharp.SpriteFontBase large, WordCard card, Rectangle box, bool alone)
    {
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold);
        DrawHelpers.Text(spriteBatch, large, card.Phrase, new Vector2(box.X + 14, box.Y + (alone ? 16 : 6)), Ink);
        var slot = alone ? new Rectangle(box.Right - 86, box.Y + 58, 72, 40) : new Rectangle(box.X + 14, box.Y + 42, 48, 16);
        spriteBatch.Draw(view.Pixel, slot, new Color(30, 30, 44));
        DrawHelpers.Border(spriteBatch, view.Pixel, slot, 1, Empty);
        DrawHelpers.Text(spriteBatch, small, "audio", new Vector2(slot.X + (alone ? 12 : 4), slot.Y + (alone ? 11 : 0)), Empty);
    }
}
