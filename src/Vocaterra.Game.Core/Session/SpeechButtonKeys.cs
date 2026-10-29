namespace Vocaterra.Game.Core.Session;

/// <summary>
/// The keys of the speech buttons and of the hold-to-talk hint as the support language knows them (S17): <see cref="GuideSession.ButtonEnglishUp"/> says whether the
/// support language next to one is drawn, and <see cref="Content.LocaleText.ButtonSupport"/> gives it.
/// </summary>
public static class SpeechButtonKeys
{
    /// <summary><c>Repetir</c>.</summary>
    public const string Repeat = "repeat";

    /// <summary><c>Más despacio</c>.</summary>
    public const string Slower = "slower";

    /// <summary><c>Ayuda</c>.</summary>
    public const string Help = "help";

    /// <summary><c>No sé</c>.</summary>
    public const string DontKnow = "dontKnow";

    /// <summary><c>Escribir</c>.</summary>
    public const string Type = "type";

    /// <summary>The hold-to-talk hint (<c>Mantén V o Espacio: hablar</c>).</summary>
    public const string Hold = "hold";

    /// <summary>Every key, in the order the buttons are drawn, the hint last.</summary>
    public static IReadOnlyList<string> All { get; } = [Repeat, Slower, Help, DontKnow, Type, Hold];
}
