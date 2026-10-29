using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Dialogue;

/// <summary>The speech buttons of the dialogue panel, their keys and the voice event each press raises (S7, D-game-9: replay, slower audio, help, "no sé" and the typed fallback are always available).</summary>
public static class SpeechButtons
{
    /// <summary>The label of <see cref="SpeechAction.Repeat"/>.</summary>
    public const string RepeatLabel = "Repetir";

    /// <summary>The label of <see cref="SpeechAction.Slower"/>.</summary>
    public const string SlowerLabel = "Más despacio";

    /// <summary>The label of <see cref="SpeechAction.Help"/>.</summary>
    public const string HelpLabel = "Ayuda";

    /// <summary>The label of <see cref="SpeechAction.DontKnow"/>.</summary>
    public const string DontKnowLabel = "No sé";

    /// <summary>The label of <see cref="SpeechAction.Type"/>.</summary>
    public const string TypeLabel = "Escribir";

    /// <summary>The fixed buttons, left to right.</summary>
    public static IReadOnlyList<SpeechButton> All { get; } =
    [
        new(SpeechAction.Repeat, RepeatLabel, "R"),
        new(SpeechAction.Slower, SlowerLabel, "S"),
        new(SpeechAction.Help, HelpLabel, "H"),
        new(SpeechAction.DontKnow, DontKnowLabel, "N"),
        new(SpeechAction.Type, TypeLabel, "W"),
    ];

    /// <summary>The English-text key of a fixed button (<see cref="SpeechButtonKeys"/>), or <see langword="null"/> for a choice.</summary>
    /// <param name="action">The button.</param>
    public static string? KeyOf(SpeechAction action) => action switch
    {
        SpeechAction.Repeat => SpeechButtonKeys.Repeat,
        SpeechAction.Slower => SpeechButtonKeys.Slower,
        SpeechAction.Help => SpeechButtonKeys.Help,
        SpeechAction.DontKnow => SpeechButtonKeys.DontKnow,
        SpeechAction.Type => SpeechButtonKeys.Type,
        _ => null,
    };

    /// <summary>
    /// The text drawn on a fixed button (S17, D-game-13): <c>[R] Repetir (repeat)</c> while the learner is early, <c>[R] Repetir</c> once the button (or its phrase) has
    /// been used <see cref="GuideSession.ButtonEnglishUses"/> times. The English is help on screen only; the key and the button work the same without it (rule 8).
    /// </summary>
    /// <param name="button">The button.</param>
    /// <param name="session">The game whose counts say whether the English is still up, or <see langword="null"/> for the Spanish alone.</param>
    public static string TextFor(SpeechButton button, GuideSession? session)
    {
        ArgumentNullException.ThrowIfNull(button);
        return session is not null && KeyOf(button.Action) is { } key && session.ButtonEnglishUp(key)
            ? $"{button.Text} ({session.English.ButtonEnglish(key)})"
            : button.Text;
    }

    /// <summary>The action a key presses while the panel shows its buttons (not while the text entry has the keys), or <see langword="null"/>.</summary>
    /// <param name="key">The key.</param>
    public static SpeechAction? ForKey(PanelKey key) => key switch
    {
        PanelKey.R => SpeechAction.Repeat,
        PanelKey.S => SpeechAction.Slower,
        PanelKey.H => SpeechAction.Help,
        PanelKey.N => SpeechAction.DontKnow,
        PanelKey.W or PanelKey.Tab => SpeechAction.Type,
        PanelKey.D1 => SpeechAction.Choice1,
        PanelKey.D2 => SpeechAction.Choice2,
        PanelKey.D3 => SpeechAction.Choice3,
        _ => null,
    };

    /// <summary>The voice event a press of the button raises (the choice buttons raise <see cref="VoiceEventKind.Choice"/> when their turn is submitted).</summary>
    /// <param name="action">The button.</param>
    public static VoiceEventKind EventOf(SpeechAction action) => action switch
    {
        SpeechAction.Repeat => VoiceEventKind.Replay,
        SpeechAction.Slower => VoiceEventKind.Slower,
        SpeechAction.Help => VoiceEventKind.Hint,
        SpeechAction.DontKnow => VoiceEventKind.NoSe,
        SpeechAction.Type => VoiceEventKind.TypeFallback,
        _ => VoiceEventKind.Choice,
    };

    /// <summary>The action of the choice at <paramref name="index"/> (0 to 2), or <see langword="null"/>.</summary>
    /// <param name="index">The position among the offered choices.</param>
    public static SpeechAction? ChoiceAt(int index) => index switch
    {
        0 => SpeechAction.Choice1,
        1 => SpeechAction.Choice2,
        2 => SpeechAction.Choice3,
        _ => null,
    };

    /// <summary>The position among the choices that a choice action stands for, or <see langword="null"/> for the fixed buttons.</summary>
    /// <param name="action">The action.</param>
    public static int? ChoiceIndex(SpeechAction action) => action switch
    {
        SpeechAction.Choice1 => 0,
        SpeechAction.Choice2 => 1,
        SpeechAction.Choice3 => 2,
        _ => null,
    };
}
