using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// Where the support pieces of S8 stand above the dialogue panel, in window pixels, as plain numbers so the drawing and the tests share them: the
/// support cards (the phrase sheet's card of each phrase at <see cref="SupportLevel.CardAndAudio"/>), the audio-only chip, and the key-word cards
/// of the stall (large and centred while they stand alone before the shopkeeper's question, small at the right of the row afterwards). The word card
/// of a pickup (<see cref="WordCardLayer"/>) lifts above <see cref="Top"/> so nothing overlaps.
/// </summary>
public readonly record struct SupportLayout
{
    /// <summary>The height of a support card.</summary>
    public const int CardHeight = 112;

    /// <summary>The widest a support card is.</summary>
    public const int CardMaxWidth = 440;

    /// <summary>The narrowest a support card is laid out.</summary>
    public const int CardMinWidth = 220;

    /// <summary>The height of the audio-only chip.</summary>
    public const int ChipHeight = 28;

    /// <summary>The width of the audio-only chip.</summary>
    public const int ChipWidth = 210;

    /// <summary>The gap between the pieces and to the panel.</summary>
    public const int Gap = 8;

    /// <summary>The width of a small key-word card.</summary>
    public const int KeyWordWidth = 150;

    /// <summary>The height of a small key-word card.</summary>
    public const int KeyWordHeight = 64;

    /// <summary>The width of a large key-word card (before the question).</summary>
    public const int LargeKeyWordWidth = 260;

    /// <summary>The height of a large key-word card.</summary>
    public const int LargeKeyWordHeight = 110;

    /// <summary>The height of the heading over the large key-word cards ("Palabras nuevas").</summary>
    public const int HeadingHeight = 24;

    /// <summary>Computes the layout for a window.</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="cards">How many support cards are up.</param>
    /// <param name="keyWords">How many key-word cards are up.</param>
    /// <param name="keyWordsAlone">Whether the key words stand alone before the question (large, centred).</param>
    /// <param name="chip">Whether the audio-only chip is up.</param>
    public SupportLayout(int viewportWidth, int viewportHeight, int cards, int keyWords, bool keyWordsAlone, bool chip)
    {
        var panel = DialogueLayout.PanelBox(viewportWidth, viewportHeight);
        var bottom = panel.Y - Gap;
        var top = panel.Y;
        Chip = null;
        if (chip)
        {
            Chip = new PixelBox(panel.X, bottom - ChipHeight, ChipWidth, ChipHeight);
            bottom = Chip.Value.Y - Gap;
            top = Chip.Value.Y;
        }

        var cardBoxes = new List<PixelBox>();
        var keyBoxes = new List<PixelBox>();
        if (keyWords > 0 && keyWordsAlone)
        {
            var total = (keyWords * LargeKeyWordWidth) + ((keyWords - 1) * Gap);
            var x = panel.X + ((panel.Width - total) / 2);
            for (var i = 0; i < keyWords; i++)
            {
                keyBoxes.Add(new PixelBox(x + (i * (LargeKeyWordWidth + Gap)), bottom - LargeKeyWordHeight, LargeKeyWordWidth, LargeKeyWordHeight));
            }

            HeadingY = bottom - LargeKeyWordHeight - HeadingHeight;
            top = Math.Min(top, HeadingY);
        }
        else
        {
            var stripWidth = keyWords == 0 ? 0 : (keyWords * KeyWordWidth) + ((keyWords - 1) * Gap);
            if (keyWords > 0)
            {
                var x = panel.Right - stripWidth;
                for (var i = 0; i < keyWords; i++)
                {
                    keyBoxes.Add(new PixelBox(x + (i * (KeyWordWidth + Gap)), bottom - KeyWordHeight, KeyWordWidth, KeyWordHeight));
                }

                top = Math.Min(top, bottom - KeyWordHeight);
            }

            if (cards > 0)
            {
                var room = panel.Width - (stripWidth == 0 ? 0 : stripWidth + Gap) - ((cards - 1) * Gap);
                var width = Math.Clamp(room / cards, CardMinWidth, CardMaxWidth);
                for (var i = 0; i < cards; i++)
                {
                    cardBoxes.Add(new PixelBox(panel.X + (i * (width + Gap)), bottom - CardHeight, width, CardHeight));
                }

                top = Math.Min(top, bottom - CardHeight);
            }
        }

        Cards = cardBoxes;
        KeyWords = keyBoxes;
        Top = top;
        KeyWordsAlone = keyWordsAlone && keyWords > 0;
    }

    /// <summary>The support cards' boxes, left to right.</summary>
    public IReadOnlyList<PixelBox> Cards { get; }

    /// <summary>The key-word cards' boxes, left to right.</summary>
    public IReadOnlyList<PixelBox> KeyWords { get; }

    /// <summary>The audio-only chip's box, or <see langword="null"/>.</summary>
    public PixelBox? Chip { get; }

    /// <summary>Whether the key words are laid out large and centred, before the question.</summary>
    public bool KeyWordsAlone { get; }

    /// <summary>The y of the heading over the large key-word cards (only when <see cref="KeyWordsAlone"/>).</summary>
    public int HeadingY { get; }

    /// <summary>The top edge of everything in the layout (the panel's top when nothing is up): other cards stay above it.</summary>
    public int Top { get; }

    /// <summary>The layout for what a session shows now, with the panel open.</summary>
    /// <param name="session">The running game.</param>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    public static SupportLayout For(GuideSession session, int viewportWidth, int viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SupportLayout(
            viewportWidth,
            viewportHeight,
            session.Support.Cards.Count,
            session.KeyWordCards.Count,
            session.KeyWordsPending,
            session.Support.AudioOnly.Count > 0 && session.Support.Cards.Count == 0);
    }
}
