using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Game.Core.Language.Speech;

/// <summary>
/// The uncertainty rule of D-game-9: an uncertain recognition asks for a retry with help, is logged as a voice event, and is never a language
/// failure. A spoken line is <b>uncertain</b> when the NPC did not understand it, or when it did but the recognizer was not sure of the words:
/// the mean of Vosk's per-word confidences, <c>[unk]</c> tokens left out, is under the threshold of the speech table (<see cref="SpeechTable.WordConfidenceThreshold"/>: 0.55 for Vosk small Spanish, measured with Kokoro speech, where correct phrases give 0.77 to 1.00, the lowest word, «sí», 0.50, and a mis-heard «hola» 0.36; a person's microphone will differ, so it is the one number to tune in the playtest; T04: it is data, keyed by ASR model, not a constant).
/// </summary>
/// <remarks>
/// <para>
/// <b>What confidence the core gives.</b> <c>AsrResult.Confidence</c> is 1.0 for the single-best decode the game asks for (core finding CR-022:
/// with an n-best list it is a softmax over near-identical hypotheses and falls under the verdict's threshold for a good phrase). The per-word
/// confidences, <c>AsrResult.Words[i].Confidence</c>, are in the result and do track the audio: Kokoro speech through the small Spanish model
/// gives 0.77 to 1.00 for the demo lines and 0.36 for «hola» heard as «gola». So the rule runs on the per-word mean, which the core already
/// exposes; no core call is missing for it. What is missing is a per-word flag for out-of-grammar speech in Constrained mode (see the S7 report):
/// Vosk gives <c>[unk]</c> a confidence of 1.0 for speech that matched nothing, so the rule also relies on <c>communicated</c>.
/// </para>
/// <para>Nothing here reads the clock or draws a random number.</para>
/// </remarks>
public static class UncertaintyRule
{
    /// <summary>
    /// How sure the recognizer was of the words of a recognition: the mean of the per-word confidences, leaving out <c>[unk]</c> (what Vosk emits
    /// for speech outside the grammar, always with a high confidence of its own). When the recognition has words and all of them are
    /// <c>[unk]</c> the result is 0; with no words at all it is the result's own confidence.
    /// </summary>
    /// <param name="recognition">The recognizer's output for the clip.</param>
    public static double MeanWordConfidence(AsrResult recognition)
    {
        ArgumentNullException.ThrowIfNull(recognition);
        if (recognition.Words.Count == 0)
        {
            return recognition.Confidence;
        }

        var real = recognition.Words.Where(static w => !string.Equals(w.Text, GrammarBuilder.UnknownToken, StringComparison.Ordinal)).ToList();
        return real.Count == 0 ? 0 : real.Average(static w => w.Confidence);
    }

    /// <summary>Whether a spoken line is uncertain: not understood, or understood on words the recognizer was not sure of.</summary>
    /// <param name="communicated">Whether the NPC understood the line.</param>
    /// <param name="confidence">The mean word confidence (<see cref="MeanWordConfidence"/>), or <see langword="null"/> for a typed line.</param>
    /// <param name="threshold">The recognizer's threshold (<see cref="SpeechTable.WordConfidenceThreshold"/>).</param>
    public static bool IsUncertain(bool communicated, double? confidence, double threshold) => !communicated || confidence < threshold;

    /// <summary>Whether a recognition the core understood is still too unsure to act on (the turn is then not completed: nothing happens in the conversation or the world).</summary>
    /// <param name="communicated">Whether the NPC would understand the line.</param>
    /// <param name="confidence">The mean word confidence.</param>
    /// <param name="threshold">The recognizer's threshold (<see cref="SpeechTable.WordConfidenceThreshold"/>).</param>
    public static bool IsTooUnsure(bool communicated, double confidence, double threshold) => communicated && confidence < threshold;
}

/// <summary>
/// The persistent line of the dialogue panel after a spoken turn, <c>Heard: «…» · Expected: «…»</c> (D-game-9: "what we heard versus what we
/// expected"): the transcript the recognizer gave, and the phrase of the phrase sheet the prompt was waiting for.
/// </summary>
/// <param name="Heard">The transcript, without the recognizer's <c>[unk]</c> marks; empty when nothing was heard.</param>
/// <param name="Expected">The expected phrase (the support card's phrase), or <see langword="null"/> when the prompt has none.</param>
/// <param name="ExpectedId">The sheet id of the expected phrase, or <see langword="null"/>.</param>
/// <param name="Unrecognized">Whether there was speech the recognizer could not make into words (in Constrained mode, speech outside the prompt's grammar comes back as <c>[unk]</c> only): the line then shows <c>«…»</c> instead of <c>nada</c>.</param>
public sealed record HeardLine(string Heard, string? Expected, string? ExpectedId, bool Unrecognized = false)
{
    /// <summary>The line as the panel shows it: <c>Heard: «quiero agua» · Expected: «Quiero agua, por favor.»</c>.</summary>
    /// <param name="ui">The support language (<c>ui.heard</c>, <c>ui.heardSomething</c>, <c>ui.heardNothing</c> and <c>ui.heardExpected</c>, C04).</param>
    public string Text(Content.LocaleText ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        var heard = Heard.Length > 0 ? ui.Ui("heard", ("heard", Heard)) : ui.Ui(Unrecognized ? "heardSomething" : "heardNothing");
        return Expected is null ? heard : heard + ui.Ui("heardExpected", ("expected", Expected));
    }

    /// <summary>
    /// The line for a turn, or <see langword="null"/> for a typed turn or a button (nothing was heard). The expected phrase is the sheet's:
    /// the row of the intent that matched, else the prompt's closest row (<see cref="PhraseSheet.Expected"/>).
    /// </summary>
    /// <param name="turn">The finished turn.</param>
    /// <param name="sheet">The phrase sheet.</param>
    public static HeardLine? For(GuideTurn turn, PhraseSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(sheet);
        if (turn.Input != InputKind.Spoken)
        {
            return null;
        }

        var row = sheet.Expected(turn.Prompt, turn.Intent, turn.Communicated, turn.Line);

        // A mean word confidence of exactly 0 means every word was [unk] (UncertaintyRule.MeanWordConfidence): something was said, not understood.
        return new HeardLine(turn.Line, row?.CardPhrase, row?.Id, turn.Line.Length == 0 && turn.Confidence is 0.0);
    }
}
