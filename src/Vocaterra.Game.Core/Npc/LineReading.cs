using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// How a line said to an NPC is read (V19, the review's R4): the listener's name said as a vocative is taken out (<see cref="Vocative"/>), the line is read, and a
/// greeting that opens it with something else to say is answered by the rest, read as a line of its own (<see cref="Greeting"/>). The typed and the spoken paths of
/// the NPCs (<see cref="NpcService"/>) and the host's CLI (<c>ConversationSession</c>) read a line here and nowhere else. They were three copies of the sequence, and
/// the review found the drift copying causes: the vocative knew only <c>hola</c> of the greetings, so <c>buenos días Luz, quiero pan</c> was not understood.
/// </summary>
public static class LineReading
{
    /// <summary>A typed line as the game reads it (see the type's summary).</summary>
    /// <typeparam name="T">What one reading gives: the analysis of phase 1 (the game) or the core's whole result (the host).</typeparam>
    /// <param name="words">The game's words (the vocative's binders, the greetings).</param>
    /// <param name="line">What the learner typed.</param>
    /// <param name="listener">The listener's name, or <see langword="null"/> for an NPC that has none (the villager, the shopkeeper).</param>
    /// <param name="prompt">The prompt the line is said at.</param>
    /// <param name="read">Reads one line at the prompt.</param>
    /// <param name="verdictOf">The verdict of a reading.</param>
    public static T Read<T>(GameWords words, string line, string? listener, PromptSpec prompt, Func<string, T> read, Func<T, Verdict> verdictOf)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(verdictOf);
        var bare = Vocative.Without(line, listener, words);
        return ByTheRest(words, read(bare), Greeting.Rest(bare, words), prompt, read, verdictOf);
    }

    /// <summary>
    /// A recognition as the game reads it (see the type's summary): the reading of what the recognizer heard is kept when the line has no vocative and no greeting
    /// before something else, and the readings without them are made from the recognition, so the recognizer is not called again.
    /// </summary>
    /// <typeparam name="T">What one reading gives: the analysis of phase 1.</typeparam>
    /// <param name="words">The game's words (the vocative's binders, the greetings).</param>
    /// <param name="heard">The reading of the line as the recognizer heard it.</param>
    /// <param name="recognitionOf">The recognition a reading was made from.</param>
    /// <param name="listener">The listener's name, or <see langword="null"/>.</param>
    /// <param name="prompt">The prompt the line is said at.</param>
    /// <param name="read">Reads one recognition at the prompt.</param>
    /// <param name="verdictOf">The verdict of a reading.</param>
    public static T Read<T>(GameWords words, T heard, Func<T, AsrResult> recognitionOf, string? listener, PromptSpec prompt, Func<AsrResult, T> read, Func<T, Verdict> verdictOf) =>
        Read(words, heard, null, recognitionOf, listener, prompt, read, verdictOf);

    /// <summary>
    /// A recognition as the game reads it, with the second decode of free talk (V31): when the reading of the first recognition (with the vocative and the greeting tried)
    /// is not taken, the recognition not chosen, <paramref name="other"/> (<c>UtteranceAnalysis.OtherPass</c>, core M80), is read the same way, and its reading is kept when
    /// it is taken. The core's pass took the whole free line or not, with the listener's name in it; here the name is out of the line, so the weakest word of what is read
    /// must meet the core's floor (<see cref="PipelineOptions.DefaultOpenPassWordFloor"/>) for a free recognition: no second, lower bar. A recognition the core chose
    /// already is not looked at twice.
    /// </summary>
    /// <typeparam name="T">What one reading gives: the analysis of phase 1.</typeparam>
    /// <param name="words">The game's words (the vocative's binders, the greetings).</param>
    /// <param name="heard">The reading of the line as the recognizer heard it (the chosen recognition).</param>
    /// <param name="other">The recognition the core did not choose, or <see langword="null"/> when one decode ran.</param>
    /// <param name="recognitionOf">The recognition a reading was made from.</param>
    /// <param name="listener">The listener's name, or <see langword="null"/>.</param>
    /// <param name="prompt">The prompt the line is said at.</param>
    /// <param name="read">Reads one recognition at the prompt.</param>
    /// <param name="verdictOf">The verdict of a reading.</param>
    public static T Read<T>(GameWords words, T heard, AsrResult? other, Func<T, AsrResult> recognitionOf, string? listener, PromptSpec prompt, Func<AsrResult, T> read, Func<T, Verdict> verdictOf)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(heard);
        ArgumentNullException.ThrowIfNull(recognitionOf);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(verdictOf);
        var whole = Vocative.Without(recognitionOf(heard), listener, words) is { } bare ? read(bare) : heard;
        var chosen = ByTheRest(words, whole, Greeting.Rest(recognitionOf(whole), words), prompt, read, verdictOf);
        if (other is null || Taken(verdictOf(chosen)))
        {
            return chosen;
        }

        var otherWhole = Vocative.Without(other, listener, words) is { } otherBare ? read(otherBare) : read(other);
        var again = ByTheRest(words, otherWhole, Greeting.Rest(recognitionOf(otherWhole), words), prompt, read, verdictOf);
        return Taken(verdictOf(again)) && MeetsFloor(recognitionOf(again)) ? again : chosen;
    }

    // A reading the NPC takes: understood, or understood off the prompt (the core's OffPromptIntent, M72).
    private static bool Taken(Verdict verdict) => verdict.Communicated || verdict.OffPromptIntent is not null;

    // The core's floor for a free recognition (M80): its weakest word is at least PipelineOptions.DefaultOpenPassWordFloor; no words, not taken.
    private static bool MeetsFloor(AsrResult recognition) =>
        recognition.Mode != RecognitionMode.Open
        || (recognition.Words.Count > 0 && recognition.Words.Min(static w => w.Confidence) >= PipelineOptions.DefaultOpenPassWordFloor);

    /// <summary>
    /// The phrase row a learner's line is, read as the NPC reads it (village-plan V20): the line as said, else without the listener's name (<c>Luz, mañana compraré pan</c>),
    /// else the rest after an opening greeting (<c>hola, mañana compraré pan</c>), each matched against the rows a test admits (<see cref="PhraseSheet.Said"/>); or
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="words">The game's words (the vocative's binders, the greetings).</param>
    /// <param name="sheet">The phrase sheet.</param>
    /// <param name="line">What the learner said.</param>
    /// <param name="listener">The listener's name, or <see langword="null"/>.</param>
    /// <param name="admits">Whether a phrase (by its id) may be the one.</param>
    public static PhraseRow? PhraseSaid(GameWords words, PhraseSheet sheet, string line, string? listener, Func<string, bool> admits)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(admits);
        var bare = Vocative.Without(line, listener, words);
        return sheet.Said(line, admits) ?? sheet.Said(bare, admits) ?? (Greeting.Rest(bare, words) is { } rest ? sheet.Said(rest, admits) : null);
    }

    // The reading of the rest after an opening greeting when it answers the turn (Greeting.AnswersRest), or the reading of the whole line.
    private static T ByTheRest<TLine, T>(GameWords words, T whole, TLine? rest, PromptSpec prompt, Func<TLine, T> read, Func<T, Verdict> verdictOf)
        where TLine : class =>
        rest is not null && read(rest) is var asked && Greeting.AnswersRest(verdictOf(whole), verdictOf(asked), prompt, words) ? asked : whole;
}
