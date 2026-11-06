namespace Vocaterra.Game.Core.Dialogue;

/// <summary>Who a line of the dialogue panel belongs to; the panel colours them differently.</summary>
public enum DialogueSpeaker
{
    /// <summary>What the player typed.</summary>
    Player,

    /// <summary>What the player said, as the recognizer heard it.</summary>
    PlayerSpoken,

    /// <summary>The verdict summary and the time of the turn (developer information, dim).</summary>
    Note,

    /// <summary>The guide's correction of the player's line (a recast, a hint or a correction).</summary>
    Correction,

    /// <summary>The note on a verb typed without the accent of its tense (M70, V21b): «compré», con tilde: es pasado. Under the reply, in the correction's colour; never spoken.</summary>
    Accent,

    /// <summary>The NPC's own reply (the guide's, the shopkeeper's or the villager's: the panel's title says whose).</summary>
    Guide,

    /// <summary>The game's line with the world facts.</summary>
    World,

    /// <summary>A hint from the game about the player's progress (for example why a word did not gain colour).</summary>
    Hint,

    /// <summary>The plain-English feedback after a turn (S16, D-game-13): what happened and what to do, drawn in its own colour and marked as English. Never spoken.</summary>
    English,

    /// <summary>The one coaching line after a turn (U02), made by the panel's rows and never stored among the lines.</summary>
    Coaching,
}
