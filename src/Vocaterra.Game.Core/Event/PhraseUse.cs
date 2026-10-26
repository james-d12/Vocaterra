using System.Globalization;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;

namespace Vocaterra.Game.Core.Event;

/// <summary>How a phrase of the phrase sheet (<c>content/PHRASES.md</c>) was used (D-game-10): the kinds S8 (phrase progress) and S11 (the session log) tell apart.</summary>
public enum PhraseUseKind
{
    /// <summary>The player answered the NPC's own question with the NPC's prompt and the help in view (the stall's "¿Qué necesitas?").</summary>
    Supported,

    /// <summary>The player said it on their own, with no question from the NPC to answer (asking the villager where the river is).</summary>
    Unaided,

    /// <summary>The player answered after the NPC offered choices (the repair ladder's third step): a recognition signal, not production.</summary>
    Choice,

    /// <summary>The player said "no sé" or asked for help: the NPC gave the model answer. Never a failure.</summary>
    DontKnow,

    /// <summary>The phrase was used with an NPC or in a situation other than the one it was taught in (the guide's "¿Quieres agua?", a request for bread at the villager).</summary>
    Transfer,

    /// <summary>The exchange ended without the phrase and the game carried on anyway: the player got what they needed without producing it. Supported, not a success.</summary>
    FailForward,
}

/// <summary>The wire names of the enumerations of the game events: stable lowercase snake case for the console and the session log.</summary>
public static class PhraseUseKindNames
{
    /// <summary>The wire name of a phrase-use kind: <c>supported</c>, <c>unaided</c>, <c>choice</c>, <c>dont_know</c>, <c>transfer</c>, <c>fail_forward</c>.</summary>
    public static string Of(PhraseUseKind kind) => kind switch
    {
        PhraseUseKind.Supported => "supported",
        PhraseUseKind.Unaided => "unaided",
        PhraseUseKind.Choice => "choice",
        PhraseUseKind.DontKnow => "dont_know",
        PhraseUseKind.Transfer => "transfer",
        PhraseUseKind.FailForward => "fail_forward",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown phrase-use kind."),
    };
}

/// <summary>
/// A phrase of the phrase sheet was used (or the game carried on without it). <see cref="PhraseId"/> is the sheet's id, resolved from the intent
/// and the prompt of the turn (<see cref="PhraseIds"/>).
/// </summary>
/// <param name="Tick">The simulation tick of the turn.</param>
/// <param name="Time">The stored time of the turn.</param>
/// <param name="PhraseId">The id of the sheet row (<c>pedir-agua</c>).</param>
/// <param name="Kind">How it was used.</param>
/// <param name="Npc">The NPC it was said to (<c>shopkeeper</c>).</param>
/// <param name="Prompt">The prompt the turn answered (<c>stall-need</c>); empty for a fail-forward with no turn.</param>
/// <param name="Line">What the player said, as sent or heard; empty for a fail-forward with no line.</param>
/// <param name="Detail">The slot that matters (the item asked for, the place), or <see langword="null"/>.</param>
/// <param name="Input">How the line arrived (typed, spoken or a button); the phrase progress applies the credit of this kind (<see cref="CreditRules"/>, S8).</param>
/// <param name="Correct">Whether the line was said without a mistake (the core's verdict had no error); a communicated line with a mistake is a slip, never a success (S8).</param>
/// <param name="Support">The support level the phrase had when it was used, before the use moved it (S8; the session log of S11 reads it).</param>
/// <param name="InPlay">For a <see cref="PhraseUseKind.DontKnow"/> use, the phrases the player did not know: the ones the NPC's prompt was asking for (S8); otherwise <see langword="null"/>.</param>
public sealed record PhraseUse(
    long Tick,
    long Time,
    string PhraseId,
    PhraseUseKind Kind,
    string Npc,
    string Prompt,
    string Line,
    string? Detail = null,
    InputKind Input = InputKind.Typed,
    bool Correct = true,
    SupportLevel Support = SupportLevel.None,
    IReadOnlyList<string>? InPlay = null)
    : GameEvent(Tick, Time)
{
    /// <inheritdoc />
    public override string Family => "phrase";

    /// <inheritdoc />
    public override string Text =>
        string.Create(CultureInfo.InvariantCulture, $"phrase {PhraseId} {PhraseUseKindNames.Of(Kind)} at {Npc}{(Detail is null ? string.Empty : $" ({Detail})")}");
}
