using Vocaterra.Game.Language.Phrases;
using Vocaterra.MonoGame.UI.Dialogue;

namespace Vocaterra.MonoGame.UI;

/// <summary>
/// The sizes of the support pieces of S8 that stand over the dialogue panel: the support cards (the phrase sheet's card of each phrase at
/// <see cref="SupportLevel.CardAndAudio"/>), the audio-only chip and the key-word cards of the stall. Their places are
/// <see cref="ConversationLayout"/>'s since S22 (the old row above the panel covered the speaker and the bubble); <see cref="ConversationCards"/> builds the requests from
/// these sizes.
/// </summary>
public static class SupportLayout
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

    /// <summary>The width of a small key-word card.</summary>
    public const int KeyWordWidth = 150;

    /// <summary>The height of a small key-word card.</summary>
    public const int KeyWordHeight = 64;
}
