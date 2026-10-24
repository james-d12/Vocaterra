namespace Vocaterra.Game.UI.Dialogue;

/// <summary>The sizes of the cards of the conversation screen (S22), as requests for <see cref="ConversationLayout"/>, and the head rectangle of a speaker.</summary>
public static class ConversationCards
{
    /// <summary>The new-word toast's width.</summary>
    private const int ToastWidth = 240;

    /// <summary>The new-word toast's height.</summary>
    private const int ToastHeight = 56;

    /// <summary>The request of the new-word toast.</summary>
    public static ConversationCardRequest NewWord { get; } = new(ConversationCardKind.NewWord, ToastWidth, ToastHeight, 180, 220);

    /// <summary>The request of a support card.</summary>
    public static ConversationCardRequest Support { get; } = new(ConversationCardKind.Support, SupportLayout.CardMaxWidth, SupportLayout.CardHeight, SupportLayout.CardMinWidth, 280);

    /// <summary>The request of the audio-only chip.</summary>
    public static ConversationCardRequest Chip { get; } = new(ConversationCardKind.Chip, SupportLayout.ChipWidth, SupportLayout.ChipHeight, SupportLayout.ChipWidth, SupportLayout.ChipWidth);

    /// <summary>The request of a key-word card.</summary>
    /// <param name="alone">Whether the key words stand alone before the question (larger, with the heading <c>Palabras nuevas</c>).</param>
    public static ConversationCardRequest KeyWord(bool alone) => alone
        ? new ConversationCardRequest(ConversationCardKind.KeyWord, 190, 92, 150, 150)
        : new ConversationCardRequest(ConversationCardKind.KeyWord, SupportLayout.KeyWordWidth, SupportLayout.KeyWordHeight, SupportLayout.KeyWordWidth, 130);

    /// <summary>
    /// The projected rectangle of a speaker's head and chest: from the top of the head to the chest, as wide as the figure's shoulders, in window pixels. The
    /// layout keeps the cards clear of it.
    /// </summary>
    /// <param name="headX">The head's x on the screen.</param>
    /// <param name="headTopY">The top of the head, y.</param>
    /// <param name="chestY">The chest's y (below the head).</param>
    public static PixelBox HeadRect(double headX, double headTopY, double chestY)
    {
        var height = Math.Max(48, (int)Math.Round(chestY - headTopY));
        var width = Math.Max(48, (int)Math.Round(height * 0.9));
        return new PixelBox((int)Math.Round(headX) - (width / 2), (int)Math.Round(headTopY), width, height);
    }
}
