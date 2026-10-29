using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Dialogue;

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

    /// <summary>The note on a card at the second rung of the ladder: the English is one key away (S16, D-game-13).</summary>
    public const string EnglishOnHelp = "H: English";

    private const int PhraseSize = 32;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Dim = UiPalette.Muted.ToColor();
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
        var frame = ConversationFrame.For(view);
        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(PhraseSize);
        var cards = session.Support.Cards;
        for (var i = 0; i < cards.Count && i < frame.Support.Count; i++)
        {
            var placement = frame.Support[i];
            if (placement.State == CardPresence.Full)
            {
                DrawCard(spriteBatch, view, small, large, cards[i], Rect(placement.Box));
            }
            else if (placement.State == CardPresence.Collapsed)
            {
                DrawLine(spriteBatch, view, small, cards[i].Phrase, Rect(placement.Box), CardBorder);
            }
        }

        var words = session.KeyWordCards;
        for (var i = 0; i < words.Count && i < frame.KeyWords.Count; i++)
        {
            var placement = frame.KeyWords[i];
            if (placement.State == CardPresence.Full)
            {
                DrawKeyWord(spriteBatch, view, small, large, words[i], Rect(placement.Box), frame.KeyWordsAlone);
            }
            else if (placement.State == CardPresence.Collapsed)
            {
                DrawLine(spriteBatch, view, small, words[i].Phrase, Rect(placement.Box), Gold);
            }
        }

        if (frame.Chip is { State: not CardPresence.Hidden } chip)
        {
            var box = Rect(chip.Box);
            spriteBatch.Draw(view.Pixel, box, Back);
            DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, Gold);
            DrawHelpers.Text(spriteBatch, small, ChipText, new Vector2(box.X + 10, box.Y + 6), Ink);
        }
    }

    /// <summary>A card collapsed to one line (S22): the phrase on a slim strip, in the card's colour.</summary>
    private static void DrawLine(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase small, string phrase, Rectangle box, Color border)
    {
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, border);
        var text = phrase;
        var room = box.Width - 16;
        while (text.Length > 1 && DrawHelpers.Width(small, text) > room)
        {
            text = text[..^2] + "…";
        }

        DrawHelpers.Text(spriteBatch, small, text, new Vector2(box.X + 8, box.Y + 4), Ink);
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
        DrawHelpers.Text(spriteBatch, small, "audio", new Vector2(slot.X + 6, slot.Y + 3), Dim);
        var phraseFont = DrawHelpers.Width(large, card.Phrase) <= width ? large : small;
        DrawHelpers.Text(spriteBatch, phraseFont, DrawHelpers.Fit(phraseFont, card.Phrase, width), new Vector2(left, box.Y + 22), Ink);
        if (!card.EnglishShown)
        {
            // Rung 2 of the ladder (S16): the Spanish card and its audio, the English on the help key.
            DrawHelpers.Text(spriteBatch, small, EnglishOnHelp, new Vector2(left, box.Bottom - 22), Dim); // it was the dim grey of the slot border, 2.7 to 1 (X08)
            return;
        }

        var english = DialoguePanel.EnglishColor;
        // The meaning wraps (a narrow card takes two rows), and the explanation or the glosses take the rows that are left above the card's bottom edge (X08).
        var meaning = TextWrap.Wrap(DialoguePanel.EnglishPrefix + card.Meaning, width, t => DrawHelpers.Width(small, t)).Take(2).ToList();
        var y = box.Y + 58;
        foreach (var row in meaning)
        {
            DrawHelpers.Text(spriteBatch, small, row, new Vector2(left, y), english);
            y += 16;
        }

        y += 2;
        var room = Math.Max(0, (box.Bottom - 4 - y) / 16);
        if (card.Explanation is { } explanation)
        {
            foreach (var row in TextWrap.Wrap(explanation, width, t => DrawHelpers.Width(small, t)).Take(Math.Min(2, room)))
            {
                DrawHelpers.Text(spriteBatch, small, row, new Vector2(left, y), english);
                y += 16;
            }
        }
        else if (card.Glosses.Count > 0)
        {
            var rows = TextWrap.Wrap(string.Join(" · ", card.Glosses), width, t => DrawHelpers.Width(small, t));
            for (var i = 0; i < Math.Min(Math.Min(2, room), rows.Count); i++)
            {
                DrawHelpers.Text(spriteBatch, small, rows[i], new Vector2(left, y + (i * 16)), Dim);
            }
        }
    }

    private static void DrawKeyWord(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase small, FontStashSharp.SpriteFontBase large, WordCard card, Rectangle box, bool alone)
    {
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, Gold);
        if (alone)
        {
            // Before the question the heading is the card's own first line (S22: it used to be a separate strip over the row).
            DrawHelpers.Text(spriteBatch, small, KeyWordsHeading, new Vector2(box.X + 14, box.Y + 6), Gold);
        }

        DrawHelpers.Text(spriteBatch, large, DrawHelpers.Fit(large, card.Phrase, box.Width - 28), new Vector2(box.X + 14, box.Y + (alone ? 24 : 6)), Ink);
        var slot = alone ? new Rectangle(box.X + 14, box.Y + 68, 52, 18) : new Rectangle(box.X + 14, box.Y + 42, 48, 16);
        spriteBatch.Draw(view.Pixel, slot, new Color(30, 30, 44));
        DrawHelpers.Border(spriteBatch, view.Pixel, slot, 1, Empty);
        DrawHelpers.Text(spriteBatch, small, "audio", new Vector2(slot.X + 4, slot.Y + (alone ? 1 : 0)), Dim);
    }
}
