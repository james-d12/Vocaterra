namespace Vocaterra.Game;

/// <summary>How a line reached the game: spoken (a recognised clip, a WAV or a synthesized line), typed, or sent by a button (a choice, <c>ayuda</c>, <c>no sé</c>).</summary>
public enum InputSource
{
    /// <summary>Spoken: the recognizer's transcript was evaluated.</summary>
    Voice,

    /// <summary>Typed on the keyboard.</summary>
    Typed,

    /// <summary>Sent by a button: a repair choice, "ayuda" or "no sé".</summary>
    Button,
}

/// <summary>Where a line was said (S19).</summary>
public enum InputPlace
{
    /// <summary>In a conversation with an NPC (the guide, the shopkeeper, the villager), outside a lesson's step.</summary>
    Dialogue,

    /// <summary>At a repeat or answer step of a lesson (<see cref="InputEntry.Unit"/> is <c>lesson/step</c>).</summary>
    Lesson,

    /// <summary>An answer of the check battery (S11); <see cref="InputEntry.Unit"/> is the item's id.</summary>
    Check,
}

/// <summary>
/// One line the learner gave the game (S19): the text the core evaluated, how it came, where it was said, what the core made of it and when. The
/// history is a list of these in the order the game took them; the word record is a fold of them (<see cref="WordRecord"/>).
/// </summary>
/// <param name="Id">The entry's number, from 1, in the order the game took the lines (it never repeats, so the order is total).</param>
/// <param name="Session">The play session the line was said in.</param>
/// <param name="StoredTime">The stored game time of the line, in Unix seconds (never the wall clock).</param>
/// <param name="Source">Voice, typed or button.</param>
/// <param name="Text">The text the core evaluated: the transcript of a spoken line, the typed line, or the button's line.</param>
/// <param name="Npc">The NPC the line was said to, or <see langword="null"/> for a check answer.</param>
/// <param name="Prompt">The bundle prompt the line was evaluated on.</param>
/// <param name="Place">Where it was said.</param>
/// <param name="Unit">For a lesson line, <c>lesson/step</c>; for a check answer, the item's id; otherwise <see langword="null"/>.</param>
/// <param name="Intent">The intent the core resolved.</param>
/// <param name="Communicated">Whether the core understood the line.</param>
/// <param name="Held">Whether the line was held back as an unsure recognition (D-game-9): kept in the history, not counted in the word record.</param>
/// <param name="Errors">The core's errors, with their spans in the normalized text, the expected form and what was heard.</param>
/// <param name="Words">The line's words with the core's readings (<see cref="GuideWord"/>); empty for a line the core read no word of.</param>
public sealed record InputEntry(
    long Id,
    int Session,
    long StoredTime,
    InputSource Source,
    string Text,
    string? Npc,
    string Prompt,
    InputPlace Place,
    string? Unit,
    string Intent,
    bool Communicated,
    bool Held,
    IReadOnlyList<GuideError> Errors,
    IReadOnlyList<GuideWord> Words);
