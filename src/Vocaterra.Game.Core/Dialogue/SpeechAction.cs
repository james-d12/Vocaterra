namespace Vocaterra.Game.Core.Dialogue;

/// <summary>What a speech button of the dialogue panel does (S7). The panel's mouse hit-test, its keys and the logic that carries them out all use these.</summary>
public enum SpeechAction
{
    /// <summary><c>Repetir</c> (R): the NPC's last line again.</summary>
    Repeat,

    /// <summary><c>Más despacio</c> (S): the NPC's last line again, slowly.</summary>
    Slower,

    /// <summary><c>Ayuda</c> (H): asks the NPC for help (sends "ayuda").</summary>
    Help,

    /// <summary><c>No sé</c> (N): says "no sé" (a <c>dont_know</c> turn, never a failure).</summary>
    DontKnow,

    /// <summary><c>Escribir</c> (W or Tab): opens the text entry.</summary>
    Type,

    /// <summary>The first offered choice (1).</summary>
    Choice1,

    /// <summary>The second offered choice (2).</summary>
    Choice2,

    /// <summary>The third offered choice (3).</summary>
    Choice3,
}
