namespace Vocaterra.Game.UI;

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

    /// <summary>The NPC's own reply (the guide's, the shopkeeper's or the villager's: the panel's title says whose).</summary>
    Guide,

    /// <summary>The game's line with the world facts.</summary>
    World,

    /// <summary>A hint from the game about the player's progress (for example why a word did not gain colour).</summary>
    Hint,

    /// <summary>The plain-English feedback after a turn (S16, D-game-13): what happened and what to do, drawn in its own colour and marked as English. Never spoken.</summary>
    English,
}
