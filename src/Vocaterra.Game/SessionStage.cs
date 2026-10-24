using Vocaterra.Game.Npc;

namespace Vocaterra.Game;

/// <summary>
/// Where the session stands, from the objective area's point of view (S17, D-game-15): the player wakes alone at their own camp and is sent down the trail to the
/// guide; the guide's lesson and the camp checklist follow; once the checklist is complete the player is sent back up the trail to light their own fire; the lit
/// fire ends the session. The stage only picks the line the objective area shows. It gates nothing (rule 8): every action works in every stage.
/// </summary>
public enum SessionStage
{
    /// <summary>The player has not met the guide yet: "Go down the trail and find the guide".</summary>
    FindGuide,

    /// <summary>The guide has been met (or the first step of the checklist is done): the camp checklist is the objective.</summary>
    Camp,

    /// <summary>The camp checklist is complete and the player's own fire is not lit: "Go back up the trail and light your own fire".</summary>
    LightFire,

    /// <summary>The checklist is complete and the player's own fire is lit: the session is complete.</summary>
    Complete,
}

/// <summary>
/// The keys of the speech buttons and of the hold-to-talk hint as the English text knows them (S17): <see cref="GuideSession.ButtonEnglishUp"/> says whether the
/// English next to one is drawn, and <see cref="EnglishText.ButtonEnglish"/> gives it.
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
